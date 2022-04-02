namespace Pulumi.Dungeon;

public static class DictionaryExtensions
{
    public static string AsString<TKey, TValue>(this Dictionary<TKey, TValue> dict) where TKey : notnull =>
        string.Join(',', dict.Select(entry => $"{entry.Key}={entry.Value}"));

    public static Dictionary<TKey, TValue> Merge<TKey, TValue>(this Dictionary<TKey, TValue> dict1, params Dictionary<TKey, TValue>[] dicts) where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>(dict1); // copy
        foreach (var (key, value) in dicts.SelectMany(dict2 => dict2))
        {
            dict[key] = value; // overwrite
        }
        return dict;
    }

    public static IList ToAsgTags<TKey, TValue>(this Dictionary<TKey, TValue> dict, bool propagateAtLaunch = true) where TKey : notnull =>
        dict.Select(tag => new { tag.Key, tag.Value, PropagateAtLaunch = propagateAtLaunch }).ToList();
}
