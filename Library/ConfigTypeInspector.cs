namespace Pulumi.Dungeon;

public sealed class ConfigTypeInspector : TypeInspectorSkeleton
{
    public ConfigTypeInspector(ITypeInspector inner)
    {
        Inner = inner;
    }

    public override string GetEnumName(Type enumType, string name) => Inner.GetEnumName(enumType, name);

    public override string GetEnumValue(object enumValue) => Inner.GetEnumValue(enumValue);

    public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object? container) =>
        Inner.GetProperties(type, container).Where(property => !Regex.IsMatch(property.Name, @"Password|Secret|Token")); // ignore secrets

    private ITypeInspector Inner { get; }
}
