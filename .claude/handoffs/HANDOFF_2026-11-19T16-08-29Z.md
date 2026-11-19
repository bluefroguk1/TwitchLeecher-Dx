# Session Handoff

- Generated: 2026-11-19T16:08:29Z
- Working directory: TwitchLeecher-Dx, branch `claude/fix-slow-downloads-15ccca` (cut from `combined`, pushed to `origin` = bluefroguk1/TwitchLeecher-Dx)

## Session goal
The user reported that VOD downloads feel slow. Find the cause in the chunk download loop, fix it, and add a Windows PowerShell script that times master against this branch. The Mac that wrote this has no .NET SDK, so the code has **not been compiled or run**. A Windows session should build it, test it and iterate.

## Decisions
- **`Parallel.ForEachAsync` replaces `Parallel.ForEach`** in `DownloadService.DownloadParts`.
  - Alternative: keep `Parallel.ForEach` and raise `ThreadPool.SetMinThreads`.
  - Rejected because that fixes only the thread starvation, not the batching. On a `List`, `Parallel.ForEach` gives each worker growing batches of up to 16 indexes, so the last chunks download on a single connection.
  - `ForEachAsync` takes one chunk at a time and keeps N requests open.
- **The outer download task stays synchronous.** It waits once with `.GetAwaiter().GetResult()` on the `ForEachAsync` task: one blocked thread instead of N.
  - Alternative: turn the `new Task<IEnumerable<string>>(...)` lambda in `Enqueue` into an async lambda.
  - Rejected because it touches the whole processing pipeline and the continuation logic.
- **DOP = `MaxConnectionCount`, not `-1`.**
  - The `-1` was a leftover from .NET Framework, where `ServicePointManager` connection limits applied; nothing sets that now.
  - The tooltip in `PreferencesView.axaml` says the setting is the number of open connections.
  - `Math.Max(1, ...)` stays as a guard against a corrupt config.
- **Cancellation token goes to `GetByteArrayAsync`, `WriteAllBytesAsync` and `Task.Delay`.**
  - The retry catch has `&& !ct.IsCancellationRequested`. Without it, a user cancel (`TaskCanceledException`) would be retried 3 times.
  - When cancelled, `ForEachAsync` throws `OperationCanceledException` with the task's token, so the outer task ends as Canceled, the same as before.
- **`HttpClient` uses `SocketsHttpHandler { PooledConnectionLifetime = 2 min }`.**
  - Long downloads re-resolve the CDN host instead of staying on one server.
  - `MaxConnectionsPerServer` (unlimited by default) and the timeout (100 s per chunk) are left as they are; neither is a bottleneck.
- **The test script times the download by watching chunk files, not with a stopwatch.**
  - It sums `^\d{8}\.ts$` in the new temp subfolder. Time runs from the first byte to the first time the peak size is reached; chunks are only deleted after the download phase.
  - ffmpeg time is excluded on purpose.

## Bugs / issues surfaced
- **Slow downloads.**
  - Symptom: VOD downloads feel slow.
  - Cause: `Parallel.ForEach` batching (single-connection tail), plus sync-over-async `GetResult()` blocking a pool thread per chunk (slow ramp-up), plus DOP 9 instead of 10.
  - Status: **fixed in commit 0ea87ad, not verified on Windows.**
- Ruled out as causes:
  - progress callbacks (a plain property set);
  - log appends (already batched);
  - per-chunk `WriteAllBytes`;
  - the ffmpeg stdin pipe (80 KB buffer, `-c copy`).
- Retry delays and the clip download (`DownloadClip`) still use blocking `WebClient`/`Thread.Sleep` in some places. They are outside the hot path and were left alone.

## Next steps
1. On Windows, from the repo root: `git fetch origin; git checkout claude/fix-slow-downloads-15ccca; dotnet build TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj -c Release`. Fix any compile errors in `DownloadService.cs` around lines 590-650 (the `ForEachAsync` lambda; `await` inside `catch` and `continue` inside `catch` are legal C#).
2. Run `powershell -ExecutionPolicy Bypass -File test-scripts\test-slow-downloads.ps1 -Rounds 2`. It builds `origin/master` in `%TEMP%\tl-speedtest-master` (a git worktree), then alternates master and branch. For every run, the user downloads the same VOD section: Source quality, a crop of 20+ minutes, the same Max connections. Results go to `%TEMP%\twitchleecher-slow-downloads-results-*.txt`.
3. If the branch is not faster, check thread and connection counts in the download log or with `dotnet-counters`. The next suspects are HTTP/2 being off (`DefaultRequestVersion`) and per-host CDN throttling. Try Max connections 15 versus 10.
4. Check that Cancel during "Downloading" ends as Canceled within seconds, without "Trying again" lines in the log.
5. Once it passes, push and consider merging into `combined` / opening a PR to upstream (ask the user first). After a passing run, remove the master worktree: `git worktree remove --force $env:TEMP\tl-speedtest-master`.

## Files touched
- `TwitchLeecher/TwitchLeecher.Services/Services/DownloadService.cs`: `ForEachAsync` chunk loop, full connection count, cancellation through the requests, and `SocketsHttpHandler` with `PooledConnectionLifetime`.
- `test-scripts/test-slow-downloads.ps1` (new): builds master and the branch, times the chunk phase automatically and reports MB/s and the speed-up, then asks y/n about playback, cancel and Max connections = 1.
- `.claude/handoffs/LATEST.md` (this file).

## Open questions
- Is 2 minutes a good `PooledConnectionLifetime`, or does reconnecting cost visible throughput on fast links? Measure if speeds dip every 2 minutes.
- Should this go into `combined` and an upstream PR, or stay a separate branch? It is the user's call.

Rules carried over: never name or credit the third-party Twitch downloader project; end commits with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; mark known limits as `// shortcut: <limit>, <when to upgrade>`; push only when the user asks.
