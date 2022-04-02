namespace Pulumi.Dungeon;

public sealed partial class ConfigCommand
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[environment]")]
        [Description("Environment name")]
        public string? Environment { get; init; }

        [CommandOption("--yaml")]
        [Description("Yaml output")]
        public bool Yaml { get; init; }
    }
}
