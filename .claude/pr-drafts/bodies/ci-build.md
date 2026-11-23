<!-- title: Add a GitHub Actions build -->
## Summary

Builds every push and pull request on Windows and Linux, and attaches a
self-contained build of each as an artifact, so changes can be tried
without a local .NET SDK.

After publishing, it starts the app (on Linux under a virtual display)
and fails if it has exited within 20 seconds, so a start-up crash is
caught before anyone downloads the build.

## Depends on

—

## Testing

The workflow runs green on the fork for every branch listed in the overview.
