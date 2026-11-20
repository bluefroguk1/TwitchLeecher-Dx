# Handoff: perf-result-badges

Result badges (QUEUED / DOWNLOADED) were recomputed for every result whenever the download queue gained or lost an item:
- a fresh default-filename build per result;
- `File.Exists` per result;
- a linear queue search per result;
- all on the UI thread.

The DOWNLOADED badge also never appeared when a download *finished*, because finishing doesn't change the queue list.

Now:
- each result's default path is cached (cleared on a new search or when Settings are saved);
- each folder is listed once per refresh, instead of a `File.Exists` per result;
- queued paths are a hash set;
- many queue events in a row cause one refresh;
- each download's state is watched, so finishing a download updates its result to DOWNLOADED.

**Test needs:** a short download.
1. Search a channel, download one result. It shows QUEUED while downloading and **DOWNLOADED when it finishes**, without searching again.
2. "Download Selected" on 10 results: the page stays responsive and all show QUEUED.
