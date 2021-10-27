using System;

namespace Pulumi.Dungeon
{
    public record StackInfo
    {
        public string ProjectName { get; init; } = default!;
        public Type StackType { get; init; } = default!;
        public string[]? Environments { get; init; }
    }
}
