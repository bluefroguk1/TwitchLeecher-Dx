# Handoff: deps-patch-updates

Safe patch updates, from `master` (own PR):
- Avalonia, Avalonia.Desktop, Fonts.Inter, Diagnostics, Themes.Fluent, Themes.Simple: 11.3.11 → 11.3.23
- Avalonia.Controls.DataGrid: 11.3.11 → 11.3.13 (its own release track; latest 11.3.x)
- Avalonia.ReactiveUI: 11.3.8 → 11.3.9
- CommunityToolkit.Mvvm: 8.4.0 → 8.4.2

Not changed on purpose: .NET stays 10 (LTS); Avalonia 12 later; Ninject, Newtonsoft.Json, icons and progress ring unchanged.

**Test needs:** a build, then a quick smoke test: search, open the download dialog, one short download, Settings, theme switch.
