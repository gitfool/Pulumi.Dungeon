using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Pulumi.Dungeon
{
    public static class DictionaryExtensions
    {
        public static string AsString<TKey, TValue>(this Dictionary<TKey, TValue> dict) where TKey : notnull =>
            string.Join(',', dict.Select(entry => $"{entry.Key}={entry.Value}"));

        public static Dictionary<TKey, TValue> Merge<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value) where TKey : notnull
        {
            dict.Add(key, value);
            return dict;
        }

        public static Dictionary<TKey, TValue> Merge<TKey, TValue>(this Dictionary<TKey, TValue> dict1, Dictionary<TKey, TValue> dict2) where TKey : notnull =>
            dict1.Union(dict2).ToDictionary(entry => entry.Key, entry => entry.Value);

        public static IList ToAsgTags<TKey, TValue>(this Dictionary<TKey, TValue> dict, bool propagateAtLaunch = true) where TKey : notnull =>
            dict.Select(tag => new { tag.Key, tag.Value, PropagateAtLaunch = propagateAtLaunch }).ToList();
    }
}
