# Handoff: "Coyote" colour theme (designed as "Eye Stripe" in the mockups) (branch `claude/feature-new-theme-645696`)

Written on a Mac with no .NET SDK, so **nothing here has been compiled or run yet**. You're on the Windows PC: build, test, fix.

## Context
- C# .NET 10, Avalonia 11.3 (SimpleTheme, `RequestedThemeVariant="Dark"`), Ninject DI.
- Repo `bluefroguk1/TwitchLeecher-Dx`. This branch is cut from `combined` (all 16 open PRs to upstream `schneidermanuel/TwitchLeecher-Dx` merged).
- Rules: never name, link or credit the third-party Twitch downloader used for ideas (code, comments, commits, docs). Push only when the user asks. End commits with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Mark known limits as `// shortcut: <limit>, <when to upgrade>`.
- The user designed the theme from photos of their fursuit character: varsity-red jacket, chocolate eye patch, turquoise eye stripe, cream sleeves.
- Mockup of the intended look: https://claude.ai/artifact/R6MAtnxzoDPGSpbnY3PnxG. The candidate palettes it was picked from: https://claude.ai/artifact/JxBtPs9JgBaYvcTTo6RmEX. Both are private to the user.

## How theming works (read before changing colours)
- Themes are `ResourceDictionary` blocks in `TwitchLeecher/TwitchLeecher/App.axaml`, keyed `New`, `Original` and `Coyote`, plus a `Shared` dictionary. `App.axaml.cs` registers them in `_themes`. `SetTheme` clears `Application.Resources` and copies in Shared plus the chosen theme. `PreferencesService.AvailableThemes` is the Settings list. Unknown names fall back to "New".
- **Only 6 of the 33 app colour keys reach the screen:**
  - `GlobalThemeColor`: every `Border`, the notification bar and the loading ring/text.
  - `WindowBackgroundGradientTop/BottomColor`: the title bar, search/download cards and, since this branch, the message box.
  - `WindowBackgroundColor`
  - `TextForegroundLightColor`: all text.
  - `TextForegroundErrorColor`: the download error box.
- The rest (`TextControl*`, `Button*`, `Scrollbar*`, …) are left over from the WPF version and unused, except `ProgressBar*`, which this branch wires up.
- **Buttons, text boxes, combo boxes, checkboxes and scrollbars use SimpleTheme's brushes** (`ThemeControlMidBrush`, `ThemeAccentBrush`, `HighlightBrush`, …). SimpleTheme defines those brushes from colours with `StaticResource`, so overriding the *Color* keys does nothing. You have to override the *Brush* keys. Coyote defines 19 of them. App resources win over the theme's own, and Original/New don't define them, so they keep SimpleTheme's defaults.

## What changed (commit 12155ab)
1. `App.axaml`:
   - The new `Coyote` dictionary: all 33 app keys, plus the 19 SimpleTheme brush overrides.
   - `Shared` gains `TlProgressBarForeground`/`TlProgressBarBackground`, built from each theme's `ProgressBarProgressColor`/`ProgressBarBackgroundColor`.
   - A global `ProgressBar` style uses them, because the download % text in `DownloadsView.axaml` is hard-coded black. **Side effect:** Original/New now show a green `#06B025` bar on a `#E6E6E6` track instead of SimpleTheme's blue on a dark track.
2. `App.axaml.cs`: `_themes.Add("Coyote", …)`.
3. `PreferencesService.cs`: `AvailableThemes` now includes `"Coyote"`.
4. `MessageBoxWindow.axaml`: the hard-coded grey gradient now uses `DynamicResource WindowBackgroundGradientTop/BottomColor`. Original/New use the same grey values, so they look unchanged.
5. `test-scripts/test-new-theme.ps1`: y/n/s walk-through of every page. Results go to `%TEMP%\twitchleecher-new-theme-results-*.txt`.

## Contrast (WCAG, checked by script)
- **Normal text, all ≥ 4.5:**
  - Text on window: 15.1
  - Text on buttons: 10.5 (6.9 when pressed)
  - Text on inputs: 16.3
  - Notification bar (cream on red `#C42A31`): 4.81
  - Selection (white on `#0F7488`): 5.42
  - Combo selected row: 8.2 (4.6 when hovered)
  - Black % on turquoise fill: 10.4
  - Black % on cream track: 16.7
- **Large text only:** red loading text on window: 3.13.
- **Constraint for future tweaks:** the notification bar draws the normal text colour on top of `GlobalThemeColor`. The accent therefore has to stay dark enough for cream text, and bright enough to read as large text on the window.
- **Don't** set `ThemeAccentBrush4` to a light colour. SimpleTheme uses it for the selected row in combo boxes, with cream text on top.

## To do on Windows
```
git fetch origin
git checkout claude/feature-new-theme-645696
powershell -ExecutionPolicy Bypass -File test-scripts\test-new-theme.ps1
```
- First check: does it build (`dotnet build TwitchLeecher\TwitchLeecher\TwitchLeecher.csproj -c Release`)?
- Ask the user for the results file and fix any FAIL. Screenshots help: compare against the mockup.

## Watch for
- **Grey controls left behind after switching** Coyote → Original → Coyote: the brush overrides are removed and re-added through `Resources.Clear()`/`Add`, and controls use `DynamicResource`, so they should refresh. If some stay stale, a restart after a theme change is the fallback.
- **Progress bar still blue:** the `ProgressBar` style in `Application.Styles` should beat SimpleTheme's ControlTheme setters. If it doesn't, check the `DynamicResource` chain `TlProgressBar*` → `ProgressBar*Color`.
- **Not themed:**
  - Download state colours: `DownloadStateToColorConverter`, hard-coded yellow/red/green.
  - The sub-only star button: `MainWindowVM`, GreenYellow/DarkGray.
  - Thumbnail badges: `#78000000` / `#EB0400`.
- **Ideas not built:**
  - Per-theme fonts, corner radius and spacing, by moving style values from `Shared`/`Application.Styles` into each theme dictionary.
  - Theme files users can load from a folder at runtime.
  - Build these only if the user asks.
