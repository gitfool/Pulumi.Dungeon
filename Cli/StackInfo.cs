namespace Pulumi.Dungeon;

public sealed record StackInfo
{
    public string ProjectName { get; init; } = null!;
    public Type StackType { get; init; } = null!;
    public string[]? Environments { get; init; }
}
