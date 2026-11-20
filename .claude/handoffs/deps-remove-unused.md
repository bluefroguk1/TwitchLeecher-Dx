# Handoff: deps-remove-unused

Removes references nothing uses, from `master` (own PR):
- `System.Data.SQLite` (Core, Services, Shared): no code uses SQLite; it ships native DLLs, so the download gets smaller.
- `System.ComponentModel.Annotations` (Core, Shared): built into .NET.
- `Microsoft.DotNet.UpgradeAssistant.Extensions.Default.Analyzers` (3 projects): leftovers from the .NET Framework migration.
- Stale `<Reference HintPath="..\..\..\..\.nuget\packages\...">` items (Gui, Shared): old Ninject 3.3.6, DataGrid 11.0.2, icons 9.0.1 and ReactiveUI 18.3.1 DLL paths from someone's machine. The real packages are already PackageReferences (directly or through project references).
- `FrameworkReference Microsoft.AspNetCore.App` (Services): no ASP.NET code; `HttpUtility`/`HttpListener` are in base .NET. This makes self-contained builds noticeably smaller.

**Test needs:** a build, then a quick smoke test: sign in (uses the localhost listener), search, one short download, Settings.
