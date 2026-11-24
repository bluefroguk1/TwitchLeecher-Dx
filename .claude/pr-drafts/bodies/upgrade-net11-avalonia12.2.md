<!-- title: Optional: upgrade to .NET 11 and Avalonia 12.2.0 -->
## Summary

An optional upgrade, separate from everything else, so it can be taken now, later or not at all without holding up a release.

- Target framework `net11.0` (Windows: `net11.0-windows10.0.17763.0`); CI uses the .NET 11 SDK.
- Avalonia 12.2.0 (core packages). DataGrid and WebView stay on 12.1.2, as there is no 12.2 release of them yet.
- Microsoft.Extensions.DependencyInjection and System.Security.Cryptography.ProtectedData 11.0.0.
- One small code change: on Avalonia 12.2 the main window's `Loaded` event also runs while the window is hidden, so the start-up token check raised the sign-in event and the handler showed the window even when the app was started minimised to the tray. The handler now only brings the window forward if it is already visible.

.NET 11 is a short-term (STS) release, but STS now has 24 months of support, so it ends around the same time as .NET 10 LTS (November 2028). Releases are self-contained, so users don't need to install anything. The machine that builds releases needs the .NET 11 SDK.

## Depends on

Everything else being merged first (it is cut from the combined build).

## Testing

Builds on Windows and Linux (GitHub Actions), the Windows native (AOT) build publishes, and the published app starts on both. The build warnings are the same as on .NET 10. Run on Windows: the sign-in check, searches (broadcasts, clips, a pasted link), a 1440p HEVC download with the chat saved as HTML, JSON and text, a clip download, cancelling a download, the log page and its Copy button, the command-line tool, the date picker pop-up, switches and drop-downs, a fresh install, the Twitch sign-in window, and starting minimised to the tray and opening the window from the tray icon.
