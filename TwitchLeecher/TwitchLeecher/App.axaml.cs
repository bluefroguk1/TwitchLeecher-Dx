using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Ninject;
using TwitchLeecher.Core.Constants;
using TwitchLeecher.Core.Events;
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
            new NamedPipeManager(Constants.PipeName).Write("Activate");

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
        _themes.Add("Coyote", (ResourceDictionary)Resources["Coyote"]);
        var themeService = _kernel.Get<IThemeService>();
        themeService.StyleChanged += (sender, args) => { SetTheme(themeService.GetTheme()); };
        var preferencesService = _kernel.Get<IPreferencesService>();
        SetTheme(string.IsNullOrEmpty(preferencesService.CurrentPreferences.Theme)
            ? "New"
            : preferencesService.CurrentPreferences.Theme);

        // re-sync at start-up in case the exe moved
        SyncAutoStart(preferencesService);
        _kernel.Get<IEventAggregator>().GetEvent<PreferencesSavedEvent>()
            .Subscribe(() => SyncAutoStart(preferencesService), true);

        // on Windows this registers the toast identity (AUMID); it must happen before any window is created
        _kernel.Get<IDesktopNotificationService>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += OnExit;
            MainWindow mainWindow = _kernel.Get<MainWindow>();

            if (Array.Exists(desktop.Args ?? [], arg => arg == AutoStart.MinimizedArg))
            {
                // the lifetime shows MainWindow when it starts; assigning it afterwards keeps it hidden in the tray
                Dispatcher.UIThread.Post(() => desktop.MainWindow = mainWindow);
            }
            else
            {
                desktop.MainWindow = mainWindow;
            }
        }

        StartTrayTooltip();

        base.OnFrameworkInitializationCompleted();
    }

    // The tray icon's tooltip says what is going on: % left on current downloads, else how many are paused,
    // else how many watched searches there are.
    private void StartTrayTooltip()
    {
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => UpdateTrayTooltip();
        timer.Start();
        UpdateTrayTooltip();
    }

    private void UpdateTrayTooltip()
    {
        try
        {
            TrayIcon icon = TrayIcon.GetIcons(this)?.FirstOrDefault();

            if (icon == null || _kernel == null)
            {
                return;
            }

            var downloads = _kernel.Get<TwitchLeecher.Services.Interfaces.IDownloadService>().Downloads.ToList();
            var active = downloads.Where(d => d.IsDownloading).ToList();
            int paused = downloads.Count(d => d.IsPaused);
            var watched = _kernel.Get<TwitchLeecher.Gui.Interfaces.IAutoDownloadService>();
            int searches = watched.Enabled ? watched.Searches.Count(w => w.Active) : 0;

            string detail;

            if (active.Count > 0)
            {
                double left = active.Average(d => 100 - d.Progress);
                detail = (active.Count == 1 ? "Downloading" : active.Count + " downloading") + " · " + left.ToString("0") + "% left";
            }
            else if (paused > 0)
            {
                detail = paused + (paused == 1 ? " paused download" : " paused downloads");
            }
            else if (searches > 0)
            {
                detail = searches + (searches == 1 ? " watched search" : " watched searches");
            }
            else
            {
                detail = null;
            }

            icon.ToolTipText = detail == null ? "Twitch Leecher DX" : "Twitch Leecher DX · " + detail;
        }
        catch
        {
            // the tooltip is cosmetic
        }
    }

    private void TrayIcon_OnClicked(object sender, EventArgs e) => _kernel.Get<MainWindow>().BringToFront();

    private void TrayOpen_OnClick(object sender, EventArgs e) => _kernel.Get<MainWindow>().BringToFront();

    private void SyncAutoStart(IPreferencesService preferencesService)
    {
        try
        {
            AutoStart.Apply(preferencesService.CurrentPreferences);
        }
        catch (Exception ex)
        {
            _kernel.Get<ILogService>().LogException(ex);
        }
    }

    private void TrayExit_OnClick(object sender, EventArgs e)
    {
        // started with --minimized and never opened: no window to close
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { Windows.Count: 0 } hidden)
        {
            hidden.Shutdown();
            return;
        }

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