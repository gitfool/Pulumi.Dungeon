using System;

namespace Pulumi.Dungeon
{
    public sealed class ResourceInfo
    {
        public string ProjectName { get; init; } = default!;
        public Type StackType { get; init; } = default!;
    }
}
