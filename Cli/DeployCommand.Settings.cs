using System.ComponentModel;
using Spectre.Console.Cli;

namespace Pulumi.Dungeon
{
    public sealed partial class DeployCommand
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<environment>")]
            [Description("Environment name")]
            public string Environment { get; init; } = default!;

            [CommandArgument(1, "[resources]")]
            [Description("Resource names; defaults to all")]
            public Resources Resources { get; init; }

            [CommandOption("--destroy")]
            [Description("Destroy stack")]
            public bool Destroy { get; init; }

            [CommandOption("--diff")]
            [Description("Show rich diff")]
            public bool Diff { get; init; }

            [CommandOption("--expect-no-changes")]
            [Description("Return error if any changes occur")]
            public bool ExpectNoChanges { get; init; }

            [CommandOption("--non-interactive")]
            [Description("Disable interactive mode")]
            public bool NonInteractive { get; init; }

            [CommandOption("-r|--refresh")]
            [Description("Refresh stack")]
            public bool Refresh { get; init; }

            [CommandOption("--repair")]
            [Description("Repair stack (interactive)")]
            public bool Repair { get; init; }

            [CommandOption("-f|--skip-preview")]
            [Description("Skip preview")]
            public bool SkipPreview { get; init; }

            [CommandOption("--target")]
            [Description("Target resource(s)")]
            public string[]? Target { get; init; }

            [CommandOption("--target-dependents")]
            [Description("Target dependent resources")]
            public bool TargetDependents { get; init; }

            [CommandOption("-y|--yes")]
            [Description("Approve update")]
            public bool Approve { get; init; }
        }
    }
}
