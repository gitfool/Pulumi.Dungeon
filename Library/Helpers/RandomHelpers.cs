namespace Pulumi.Dungeon;

public static class RandomHelpers
{
    public static string GetNameSuffix()
    {
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}
