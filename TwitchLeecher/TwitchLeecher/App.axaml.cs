using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
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

    private IServiceProvider _services;
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

    private IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();

        RegisterTypes(services);
        LoadModules(services);

        // ValidateOnBuild: a missing registration fails at start-up, not when a page first opens
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private void RegisterTypes(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowVM>();
        services.AddSingleton<IEventAggregator, EventAggregator>();
    }

    private void LoadModules(IServiceCollection services)
    {
        GuiModule.Load(services);
        ServiceModule.Load(services);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _services = CreateServices();

        _sharedResources = (ResourceDictionary)Resources["Shared"];
        _themes.Add("New", (ResourceDictionary)Resources["New"]);
        _themes.Add("Original", (ResourceDictionary)Resources["Original"]);
        var themeService = _services.GetRequiredService<IThemeService>();
        themeService.StyleChanged += (sender, args) => { SetTheme(themeService.GetTheme()); };
        var preferencesService = _services.GetRequiredService<IPreferencesService>();
        SetTheme(string.IsNullOrEmpty(preferencesService.CurrentPreferences.Theme)
            ? "New"
            : preferencesService.CurrentPreferences.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += OnExit;
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
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