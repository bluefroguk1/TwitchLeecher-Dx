<!-- title: Remove ReactiveUI -->
## Summary

ReactiveUI was only used in five places:
- three `ReactiveCommand.CreateFromTask` commands (download, undo and defaults)
- `BindableBase` deriving from `ReactiveObject`
- `.UseReactiveUI()` at start-up

`BindableBase` already raises its own `PropertyChanged`, so `ReactiveObject` only added a second, hidden event (compiler warning CS0108). The three commands now use the project's own `DelegateCommand.FromAsyncHandler`, which also awaits the task instead of dropping it. Each of those methods catches its own errors.

`ApiService` had leftover `System.Reactive.Linq` and `DynamicData` usings that came in with ReactiveUI. The only use was DynamicData's `AddRange` on an `ObservableCollection`, which is now a plain loop.

Why: `Avalonia.ReactiveUI` isn't published for Avalonia 12, and ReactiveUI's reflection doesn't suit trimming or Native AOT.

## Depends on

—

## Testing

- Builds on Windows and Linux (GitHub Actions), and the published app starts on both.
