## What

Adds a direct reference to `Tmds.DBus.Protocol` 0.21.3 in `TwitchLeecher.Shared`.

## Why

Avalonia 11.3 brings in `Tmds.DBus.Protocol` 0.21.2 on Linux, which has a high-severity advisory ([GHSA-xrw6-gwf8-vvr9](https://github.com/advisories/GHSA-xrw6-gwf8-vvr9)). The restore shows it as warning NU1903. Pinning the patched version in the shared project carries it to every project through the existing project references.

## Testing

- Builds on Windows and Linux (GitHub Actions).
- The NU1903 warning no longer appears.

If this is merged after the Avalonia patch-update PR, the pin can be dropped once Avalonia's own dependency moves to 0.21.3 or later.
