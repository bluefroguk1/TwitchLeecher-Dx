using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TwitchLeecher.Services.Services
{
    // Reads Twitch and GitHub responses with System.Text.Json the way the code read them with Newtonsoft's JToken:
    // a missing key or a JSON null gives default(T), and numbers, strings and booleans convert into each other.
    internal static class JsonNodeExtensions
    {
        public static JsonObject ParseObject(string json) => JsonNode.Parse(json)?.AsObject();

        public static JsonArray ParseArray(string json) => JsonNode.Parse(json)?.AsArray();

        public static JsonNode Parse(string json) => JsonNode.Parse(json);

        public static T Value<T>(this JsonNode node, string key) => node is JsonObject obj ? Convert<T>(obj[key]) : default;

        public static T Value<T>(this JsonNode node) => Convert<T>(node);

        // "$.a.b.c" through object properties, which is all these responses need; null when any part is missing
        public static JsonNode SelectToken(this JsonNode node, string path, bool errorWhenNoMatch = false)
        {
            foreach (string part in path.TrimStart('$').Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                node = node is JsonObject obj ? obj[part] : null;

                if (node == null)
                {
                    return null;
                }
            }

            return node;
        }

        public static bool HasValues(this JsonNode node) =>
            node is JsonObject obj ? obj.Count > 0 : node is JsonArray array && array.Count > 0;

        private static T Convert<T>(JsonNode node)
        {
            if (node == null)
            {
                return default;
            }

            // JsonNode, JsonObject, JsonArray
            if (node is T same)
            {
                return same;
            }

            if (node is not JsonValue value)
            {
                return default;
            }

            JsonElement e = value.TryGetValue(out JsonElement element)
                ? element
                : JsonDocument.Parse(value.ToJsonString()).RootElement;

            object result;

            if (typeof(T) == typeof(string))
            {
                result = e.ValueKind == JsonValueKind.Null ? null : e.ToString();
            }
            else if (typeof(T) == typeof(int))
            {
                result = e.ValueKind switch
                {
                    JsonValueKind.Number => e.TryGetInt32(out int i) ? i : (int)e.GetDouble(),
                    JsonValueKind.String => int.TryParse(e.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? s : 0,
                    _ => 0
                };
            }
            else if (typeof(T) == typeof(double))
            {
                result = e.ValueKind switch
                {
                    JsonValueKind.Number => e.GetDouble(),
                    JsonValueKind.String => double.TryParse(e.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0d,
                    _ => 0d
                };
            }
            else if (typeof(T) == typeof(bool))
            {
                result = e.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.String => bool.TryParse(e.GetString(), out bool b) && b,
                    JsonValueKind.Number => e.GetDouble() != 0,
                    _ => false
                };
            }
            else
            {
                throw new NotSupportedException("Value<" + typeof(T).Name + "> is not supported");
            }

            return (T)result;
        }
    }
}
