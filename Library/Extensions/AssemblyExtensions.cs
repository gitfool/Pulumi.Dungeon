namespace Pulumi.Dungeon;

public static class AssemblyExtensions
{
    public static string ReadResource(this Assembly assembly, Type type, string directory, string name)
    {
        using var stream = assembly.GetManifestResourceStream(type, $"{directory}.{name}");
        if (stream == null)
        {
            throw new InvalidOperationException($"Failed to find resource {type.Namespace}.{directory}.{name}");
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().NormalizeNewLines();
    }
}
