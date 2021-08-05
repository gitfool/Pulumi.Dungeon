using System.ComponentModel;
using Spectre.Console.Cli;

namespace Pulumi.Dungeon
{
    public sealed partial class ConfigCommand
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[environment]")]
            [Description("Environment name")]
            public string? Environment { get; init; }
        }
    }
}
