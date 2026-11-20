# Handoff: perf-parallel-chat

Chat was fetched one GQL page at a time, each page starting where the last one ended. A long VOD's chat took thousands of requests in a row.

`GetChatMessages` now:
- splits the range into **10-minute segments**, with no overlap: a non-last segment stops one second before the next starts;
- fetches **up to 4 at once** with the existing page loop (now `GetChatSegment`);
- joins them in time order.

Progress reports the seconds covered across all segments. A cancel stops all segments. One segment's error fails the chat step as before: a warning, and the video is kept. Ranges up to 10 minutes are fetched exactly as before.

`// shortcut:` fixed segment length and parallelism; raise them if Twitch allows more without throttling.

**Test needs:** a download with chat of at least an hour of a busy VOD, to compare times.
1. Save chat as text for the same 1–2 hour range on `combined-all` before and after this change. It should be clearly faster.
2. Line count and first/last lines are the same (allow a handful of differences at the exact segment boundaries).
3. Timestamps never go backwards (the chat test script checks this).
4. Cancel during "Saving Chat" stops within seconds.
