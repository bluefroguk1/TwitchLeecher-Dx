<!-- title: Read JSON with System.Text.Json instead of Newtonsoft.Json -->
## Summary

The app read Twitch and GitHub responses with Newtonsoft.Json, currently pinned to a pre-release (13.0.5-beta1). They are now read with .NET's built-in System.Text.Json (`JsonNode`), and the Newtonsoft.Json package is removed.

- A small `JsonNodeExtensions` keeps the reads exactly as they were written: `Value<T>("key")`, `SelectToken("$.a.b")` and `HasValues`, with the same lenient conversions (a missing key or a JSON null gives the default; numbers, strings and booleans convert into each other). So each call site only changes its type names (`JObject` → `JsonObject`, `JToken` → `JsonNode`, `JArray` → `JsonArray`).
- The four places that looped over a token directly now call `.AsArray()` first, because a `JsonNode` isn't enumerable the way a `JToken` was.
- Dates: Newtonsoft turned date strings into culture-formatted dates before the code parsed them again. The code now parses the original ISO string, which gives the same moment in time.
- One less package, faster parsing, and it works with Native AOT: in the Windows native build, the AOT warnings went from 7 to 1.

## Depends on

The HttpClient change (#31): both touch the same API code, so this is stacked on it. Please review only the last commit.

## Testing

Builds on Windows and Linux (GitHub Actions), and the published app starts on both. Not yet run on Windows.
