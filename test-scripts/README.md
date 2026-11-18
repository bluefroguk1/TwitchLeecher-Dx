# Test scripts

Manual test scripts for the branches behind the pull requests to schneidermanuel/TwitchLeecher-Dx.
Each builds the app, launches it, walks through the checks (y = pass, n = fail, s = skip) and saves a results file to `%TEMP%`.

Run from the repo root with the matching branch checked out:

    powershell -ExecutionPolicy Bypass -File test-scripts\<script>.ps1        (add -SkipBuild to reuse a build)

| Script | PR branch | PR |
|---|---|---|
| test-bugfixes | bugfixes-pr | #104 |
| test-feature-48-hevc | feature-48-hevc-pr | #105 |
| test-feature-37-clips | feature-37-clips-pr | #106 |
| test-feature-71-chat | feature-71-chat-pr | #107 |
| test-feature-96-qol | feature-96-qol-pr | #108 |
| test-feature-requests | feature-requests-pr | #109 |
| test-ffmpeg-update | ffmpeg-update-pr | #110 |
| test-direct-to-ffmpeg | direct-to-ffmpeg-pr | #111 |
| test-fix-search-error | fix-search-error-pr | #112 |
| test-fix-page-scroll | fix-page-scroll-pr | #113 |
| test-perf-rate-limit | perf-rate-limit-pr | #114 |
| test-perf-log-append | perf-log-append-pr | #115 |
| test-perf-lazy-thumbnails | perf-lazy-thumbnails-pr | #116 |
| test-feature-50-vod-info | feature-50-vod-info-pr | #117 |
| test-feature-86-default-favourites | feature-86-default-favourites-pr | #118 |
| test-feature-35-size-estimate | feature-35-size-estimate-pr | #119 |

The scripts live on the original (non-`-pr`) branches' history only in this branch now; the PR branches do not contain them.
Settings such as "Quick download" and "Disable conversion" persist across branches, so reset them between runs.
