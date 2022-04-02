namespace Pulumi.Dungeon;

public static class ObjectExtensions
{
    public static Dictionary<string, object> ToDictionary(this object obj) => obj.ToJson().DeserializeJson();

    public static string ToJson(this object obj, bool writeIndented = true) => JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = writeIndented }).NormalizeNewLines();

    public static IEnumerable<KeyValuePair<string, object?>> ToTokens(this object obj, string? prefix = null)
    {
        static IEnumerable<IEnumerable<KeyValuePair<string, object?>>> GetTokens(string key, object? value)
        {
            var type = value != null ? Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType() : null!;
            if (value == null || IsLeafType(type) || value is ICollection { Count: 0 })
            {
                yield return Enumerable.Repeat(new KeyValuePair<string, object?>(key, value), 1);
            }
            else if (value is IList list) // unroll list tokens
            {
                for (var i = 0; i < list.Count; i++)
                {
                    yield return GetTokens($"{key}[{i}]", list[i]).SelectMany(tokens => tokens);
                }
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && type.GetGenericArguments()[0] == typeof(string)) // unroll string dictionary tokens
            {
                var keyValueType = typeof(KeyValuePair<,>).MakeGenericType(type.GetGenericArguments());
                foreach (var entry in (IEnumerable)value)
                {
                    var entryKey = keyValueType.InvokeMember("Key", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty, null, entry, null) as string;
                    var entryValue = keyValueType.InvokeMember("Value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty, null, entry, null);
                    var asProperty = Regex.IsMatch(entryKey!, @"^[A-Z][A-Za-z]*$");
                    yield return GetTokens(asProperty ? $"{key}.{entryKey}" : $"{key}['{entryKey}']", entryValue).SelectMany(tokens => tokens);
                }
            }
            else
            {
                yield return value.ToTokens(key);
            }
        }

        object? GetValue(PropertyInfo property)
        {
            try
            {
                return property.GetValue(obj);
            }
            catch (TargetInvocationException) // when (ex.InnerException is ArgumentException || ex.InnerException is FormatException || ex.InnerException is TypeInitializationException)
            {
                return null; // ignore
            }
        }

        static bool IsLeafType(Type type) =>
            type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(string) || type == typeof(byte[]) || // extended primitives
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid) || type == typeof(Uri) ||
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && type.GetGenericArguments()[0] != typeof(string); // non-string dictionary

        return obj.GetType()
            .GetProperties()
            .Where(property => property.GetIndexParameters().Length == 0) // filter indexed properties
            .SelectMany(property => // flatten nested properties
            {
                var key = prefix != null ? $"{prefix}.{property.Name}" : property.Name;
                return GetTokens(key, GetValue(property)).SelectMany(tokens => tokens); // flatten nested tokens
            });
    }

    public static string ToValueString(this object? value)
    {
        var type = value?.GetType();
        return value switch
        {
            null => "(null)",
            bool boolean => boolean.ToString().ToLowerInvariant(),
            string { Length: 0 } => @"""""",
            IList { Count: 0 } => "[]",
            IDictionary { Count: 0 } => "{}",
            _ when type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) => "{...}", // roll-up non-string dictionary
            _ => value.ToString()!
        };
    }

    public static string ToYaml(this object obj, Action<SerializerBuilder>? configure = null)
    {
        using var writer = new StringWriter { NewLine = "\n" };
        var builder = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections | DefaultValuesHandling.OmitNull)
            .DisableAliases()
            .WithIndentedSequences();
        configure?.Invoke(builder);
        var serializer = builder.Build();
        serializer.Serialize(writer, obj);
        return writer.ToString();
    }
}
