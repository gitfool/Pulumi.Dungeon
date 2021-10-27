using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace Pulumi.Dungeon
{
    public static class StringExtensions
    {
        public static Dictionary<string, object> DeserializeJson(this string json) => JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

        public static Dictionary<string, object> DeserializeYaml(this string yaml) => new DeserializerBuilder().Build().Deserialize<Dictionary<string, object>>(yaml);

        public static string ToBase64(this string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        public static string ToCamelCase(this string value)
        {
            if (string.IsNullOrEmpty(value) || !char.IsUpper(value[0]))
            {
                return value;
            }

            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (i == 1 && !char.IsUpper(chars[i]))
                {
                    break;
                }
                if (i > 0 && i + 1 < chars.Length && !char.IsUpper(chars[i + 1]))
                {
                    if (chars[i + 1] == ' ')
                    {
                        chars[i] = char.ToLowerInvariant(chars[i]);
                    }
                    break;
                }
                chars[i] = char.ToLowerInvariant(chars[i]);
            }

            return new string(chars);
        }
    }
}
