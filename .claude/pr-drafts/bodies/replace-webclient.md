<!-- title: Replace WebClient with one shared HttpClient (#31) -->
## Summary

Closes #31 (the HttpClient part). `WebClient` is obsolete (24 SYSLIB0014 warnings) and opened a new connection for every request.

- A small `HttpRequestClient` replaces it. It keeps the per-request headers and query string that `WebClient` had, so the API code reads almost the same, and sends everything through **one shared `HttpClient`**, so connections are reused instead of opened per request.
- It uses the synchronous `HttpClient.Send`, so callers stay as they are, with no sync-over-async. Errors keep their meaning: an HTTP error status still arrives with its status code (401 on the token check, 404 on a missing video), and a network failure or timeout arrives without one.
- POST bodies are sent as plain UTF-8 with no Content-Type of their own, as `WebClient.UploadString` did.

Fixes in the video chunk download, found while replacing it:
- A chunk that kept failing was retried **forever** once its three retries were used up. It now fails the download instead.
- Warnings were added to a `List<string>` from several threads at once. It's now a `ConcurrentBag`.
- Cancel now aborts requests that are in flight, and still ends as "Canceled", not "Error".
- Each chunk is written to a `.part` file and renamed, so a chunk on disk is always complete.
- A connection count of 1 no longer makes the parallel loop throw.

The API methods stay synchronous; making the whole service async would change every caller and is better done as its own step.

## Depends on

—

## Testing

Builds on Windows and Linux (GitHub Actions), and the published app starts on both, with no SYSLIB0014 warnings left.

Run on Windows: the sign-in token check, channel search (broadcasts and clips), pasting a video URL, a clip download (plays), a 2-minute VOD crop with chat (HTML) and video details, cancelling a running VOD download (ends as "Canceled"), and the command-line `search` and `qualities` with their exit codes all work.

Not yet confirmed on Windows: logging out (the revoke request), and emote pictures in the HTML chat (the chat sampled had no emotes). (Update this section once they have been run.)
