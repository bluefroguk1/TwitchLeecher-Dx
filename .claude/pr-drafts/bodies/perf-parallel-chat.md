<!-- title: Chat export: fetch long ranges in parallel -->
## Summary

Chat pages were fetched one at a time. Ranges longer than 10 minutes are now split into 10-minute segments (no overlap), fetched up to 4 at once and joined in order; progress and cancellation cover all segments.

## Depends on

the chat export PR

Until that is merged this PR also shows its changes; please review the last commit only.

## Testing

Not yet tested on Windows.
