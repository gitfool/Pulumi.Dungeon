namespace Pulumi.Dungeon;

public sealed partial class DeployCommand
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<environment>")]
        [Description("Environment name")]
        public string Environment { get; init; } = null!;

        [CommandArgument(1, "[stacks]")]
        [Description("Stack names; defaults to all")]
        public Stacks Stacks { get; init; }

        [CommandOption("--destroy")]
        [Description("Destroy stack resources")]
        public bool Destroy { get; init; }

        [CommandOption("--diff")]
        [Description("Show rich diff")]
        public bool Diff { get; init; }

        [CommandOption("--expect-no-changes")]
        [Description("Return error if any changes occur")]
        public bool ExpectNoChanges { get; init; }

        [CommandOption("--log-events")]
        [Description("Log engine events")]
        public bool LogEvents { get; init; }

        [CommandOption("--non-interactive")]
        [Description("Disable interactive mode")]
        public bool NonInteractive { get; init; }

        [CommandOption("-r|--refresh")]
        [Description("Refresh stack resources")]
        public bool Refresh { get; init; }

        [CommandOption("--remove")]
        [Description("Remove stack")]
        public bool Remove { get; init; }

        [CommandOption("--repair")]
        [Description("Repair stack resources (interactive)")]
        public bool Repair { get; init; }

        [CommandOption("-f|--skip-preview")]
        [Description("Skip preview")]
        public bool SkipPreview { get; init; }

        [CommandOption("--target")]
        [Description("Target stack resource(s)")]
        public string[]? Target { get; init; }

        [CommandOption("--target-dependents")]
        [Description("Target dependent stack resources")]
        public bool TargetDependents { get; init; }

        [CommandOption("--unprotect")]
        [Description("Unprotect stack resources")]
        public bool Unprotect { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Approve automatically")]
        public bool Approve { get; init; }
    }
}
