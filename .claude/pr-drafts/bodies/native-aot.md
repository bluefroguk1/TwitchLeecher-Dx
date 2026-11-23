<!-- title: Publish Windows builds as Native AOT -->
## Summary

Windows publishes now compile to a native exe. That gives a faster start-up and lower memory use, and there's no .NET runtime to install. Linux publishes are unchanged.

Fixes for code that relied on runtime features that AOT removes:
- `ffmpeg.exe` is found from `AppContext.BaseDirectory`. `Assembly.Location` is empty in a native exe.
- `GetDescription()` reads the enum field via `typeof(T)`, annotated so the trimmer keeps the fields and their `Description` attributes.
- `Marshal.SizeOf<T>()` replaces `Marshal.SizeOf(typeof(T))`.
- The Gui project now compiles its bindings (every view already declares `x:DataType`). Reflection bindings can silently show nothing once trimmed.
- `BuiltInComInteropSupport` is removed. AOT doesn't support it, and Avalonia 11 doesn't use it.

## Depends on

The Ninject and ReactiveUI removals. Both rely on runtime reflection. This draft includes them as its first two commits, so please review only the last one.

## Testing

- The native Windows exe publishes on GitHub Actions and starts.
- Linux builds and starts as before.
- Remaining AOT warnings come from Newtonsoft.Json and System.Linq.Expressions. The app only uses `JObject`/`JToken` parsing and compiled `CanExecute` expressions, which both run under AOT.
