<!-- title: Replace the icon package with a small built-in Icon control -->
## Summary

`Projektanker.Icons.Avalonia` is no longer maintained. Its last release was in May 2025, and the company behind it has closed. It also crashes on Avalonia 12 with a `MissingMethodException` (see its open issue #232).

The app uses 27 Font Awesome icons, so they now live in `TwitchLeecher.Gui/Controls/IconData.cs`:
- They are the same Font Awesome Free 6.7.2 shapes as before (CC BY 4.0, credited in the README).
- A small `Icon` control draws them the way the old one did: a FontSize square, the icon fitted and centred, in the inherited text colour.
- `Value="fa-solid fa-…"` keeps working. The XAML only changes the namespace that the existing `avalonia:` prefix points to.
- It works on Avalonia 11 and 12, and uses no runtime reflection, so it also suits Native AOT.
- To add an icon, paste its view box size and SVG path into `IconData`. An unknown name shows nothing, and asserts in Debug builds.

## Depends on

—

## Testing

Tested on Windows 11 (2026-11-23): every page in the dark and light themes: icons show at the right size, centred and in the right colour.
