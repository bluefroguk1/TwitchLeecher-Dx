## What

Replaces Ninject with the built-in .NET container, `Microsoft.Extensions.DependencyInjection`.

- `ServiceModule` and `GuiModule` keep their place and their registrations. `Bind<I>().To<C>().InSingletonScope()` becomes `AddSingleton<I, C>()`.
- The view models that Ninject created without a binding are now registered as transient, so each page still gets a new instance as before.
- `DialogService`, `NavigationService` and `NotificationService` take `IServiceProvider` in place of `IKernel`, and call `GetRequiredService<T>()` in place of `Get<T>()`.
- `ValidateOnBuild` is on, so a missing registration stops the app at start-up instead of when a page first opens.
- Removes the Ninject package from Core, Services and Shared, and two stale `Ninject.dll` references with local HintPaths from the Gui project.

## Why

- Ninject 4.0.0-beta.1 is a 2017 beta that is no longer maintained.
- Ninject creates objects by runtime reflection, which blocks trimming and Native AOT. AOT would give a faster start-up and lower memory use, and makes a single exe possible. The Microsoft container is AOT-compatible.
- It's already part of the .NET ecosystem, so this adds no new third-party dependency.

## Testing

- Builds on Windows and Linux (GitHub Actions).
- Every registered class's constructor parameters were checked against the registrations.
