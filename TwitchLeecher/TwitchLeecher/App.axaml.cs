using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Ninject;
using TwitchLeecher.Gui.Modules;
using TwitchLeecher.Gui.ViewModels;
using TwitchLeecher.Gui.Views;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Services.Modules;
using TwitchLeecher.Services.Services;
using TwitchLeecher.Shared.Communication;
using TwitchLeecher.Shared.Events;

namespace TwitchLeecher;

public partial class App : Application
{
    private readonly Mutex _singletonMutex;

    private IKernel _kernel;
    private Dictionary<string, ResourceDictionary> _themes;
    private ResourceDictionary _sharedResources;

    public App()
    {
        _themes = new Dictionary<string, ResourceDictionary>();
        _singletonMutex = new Mutex(true, "TwitchLeecher", out bool isOnlyInstance);

        if (!isOnlyInstance)
        {
            new NamedPipeManager("TwitchLeecher").Write("Activate");

            Environment.Exit(0);
        }
    }

    private void OnExit(object? sender,
        ControlledApplicationLifetimeExitEventArgs controlledApplicationLifetimeExitEventArgs)
    {
        if (_singletonMutex != null)
        {
            _singletonMutex.Dispose();
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private IKernel CreateKernel()
    {
        IKernel kernel = new StandardKernel();

        RegisterTypes(kernel);
        LoadModules(kernel);

        return kernel;
    }

    private void RegisterTypes(IKernel kernel)
    {
        kernel.Bind<MainWindow>().ToSelf().InSingletonScope();
        kernel.Bind<MainWindowVM>().ToSelf().InSingletonScope();
        kernel.Bind<IEventAggregator>().To<EventAggregator>().InSingletonScope();
        kernel.Bind<IDesktopNotificationService>().To<DesktopNotificationService>().InSingletonScope();
    }

    private void LoadModules(IKernel kernel)
    {
        kernel.Load<GuiModule>();
        kernel.Load<ServiceModule>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _kernel = CreateKernel();

        _sharedResources = (ResourceDictionary)Resources["Shared"];
        _themes.Add("New", (ResourceDictionary)Resources["New"]);
        _themes.Add("Original", (ResourceDictionary)Resources["Original"]);
        var themeService = _kernel.Get<IThemeService>();
        themeService.StyleChanged += (sender, args) => { SetTheme(themeService.GetTheme()); };
        var preferencesService = _kernel.Get<IPreferencesService>();
        SetTheme(string.IsNullOrEmpty(preferencesService.CurrentPreferences.Theme)
            ? "New"
            : preferencesService.CurrentPreferences.Theme);

        // on Windows this registers the toast identity (AUMID); it must happen before any window is created
        _kernel.Get<IDesktopNotificationService>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += OnExit;
            desktop.MainWindow = _kernel.Get<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TrayIcon_OnClicked(object sender, EventArgs e) => _kernel.Get<MainWindow>().BringToFront();

    private void TrayOpen_OnClick(object sender, EventArgs e) => _kernel.Get<MainWindow>().BringToFront();

    private void TrayExit_OnClick(object sender, EventArgs e)
    {
        // same path as the close button: saves window state, then the app exits with its last window
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Close();
    }

    private void SetTheme(string name)
    {
        Resources.Clear();

        foreach (var sharedResouce in _sharedResources)
        {
            Resources.Add(sharedResouce);
        }

        foreach (var themeResource in _themes[name])
        {
            Resources.Add(themeResource);
        }
    }
}