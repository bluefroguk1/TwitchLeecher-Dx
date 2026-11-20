using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.ViewModels;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Communication;

namespace TwitchLeecher.Gui.Views
{
    public partial class MainWindow : Window
    {
        #region Fields

        private readonly IDialogService _dialogService;
        private readonly IRuntimeDataService _runtimeDataService;
        private readonly IPreferencesService _preferencesService;
        private readonly NamedPipeManager _namedPipeManager;

        private bool _shown;

        private bool _trayPromptOpen;

        #endregion Fields

        #region Constructors

        public MainWindow(
            MainWindowVM viewModel,
            IDialogService dialogService,
            IRuntimeDataService runtimeDataService,
            IPreferencesService preferencesService)
        {
            _dialogService = dialogService;
            _runtimeDataService = runtimeDataService;
            _preferencesService = preferencesService;

            _namedPipeManager = new NamedPipeManager("TwitchLeecher-DX");
            _namedPipeManager.OnMessage += OnPipeMessage;
            _namedPipeManager.StartServer();

            InitializeComponent();

            SizeChanged += (s, e) =>
            {
                if (WindowState == WindowState.Normal)
                {
                    WidthNormal = Width;
                    HeightNormal = Height;
                }
            };

            PositionChanged += (s, e) =>
            {
                if (WindowState == WindowState.Normal)
                {
                    TopNormal = Position.Y;
                    LeftNormal = Position.X;
                }
            };

            Loaded += (s, e) =>
            {
                DataContext = viewModel;

                if (viewModel != null)
                {
                    viewModel.Loaded();
                }

                LoadWindowState();
            };

            PropertyChanged += (s, e) =>
            {
                if (e.Property != WindowStateProperty || WindowState != WindowState.Minimized)
                {
                    return;
                }

                Preferences preferences = _preferencesService.CurrentPreferences;

                // the tray icon in App.axaml brings the window back
                if (preferences.AppMinimiseToTray)
                {
                    Hide();
                }
                else if (!preferences.AppTrayPromptShown && !_trayPromptOpen)
                {
                    // let the minimise finish first
                    Dispatcher.UIThread.Post(AskAboutTray);
                }
            };

            Activated += (s, e) =>
            {
                if (viewModel != null && !_shown)
                {
                    _shown = true;
                    viewModel.Shown();
                }
            };

            Closed += (s, e) =>
            {
                SaveWindowState();

                _namedPipeManager.StopServer();
            };
        }

        #endregion Constructors

        #region Properties

        public double WidthNormal { get; set; }

        public double HeightNormal { get; set; }

        public double TopNormal { get; set; }

        public double LeftNormal { get; set; }

        #endregion Properties

        #region Methods

        private void OnPipeMessage(string message)
        {
            if (message == "Activate")
            {
                BringToFront();
            }
        }

        public void BringToFront()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                // the window may be hidden in the tray
                Show();

                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }

                this.Topmost = true;

                this.Activate();

                Dispatcher.UIThread.InvokeAsync(() => { this.Topmost = false; });
            });
        }

        private void AskAboutTray()
        {
            _trayPromptOpen = true;

            // no owner: an owned dialog would be minimised along with this window
            TrayPromptWindow prompt = new TrayPromptWindow();

            prompt.Closed += (s, e) =>
            {
                _trayPromptOpen = false;

                if (prompt.UseTray is not bool useTray)
                {
                    return;
                }

                try
                {
                    Preferences preferences = _preferencesService.CurrentPreferences.Clone();
                    preferences.AppMinimiseToTray = useTray;
                    preferences.AppTrayPromptShown = prompt.DontAskAgainChecked;
                    _preferencesService.Save(preferences);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowAndLogException(ex);
                }

                if (useTray && WindowState == WindowState.Minimized)
                {
                    Hide();
                }
            };

            prompt.Show();
        }

        public void LoadWindowState()
        {
            try
            {
                MainWindowInfo mainWindowInfo = _runtimeDataService.RuntimeData.MainWindowInfo;

                if (mainWindowInfo != null)
                {
                    Width = Math.Max(MinWidth, mainWindowInfo.Width);
                    Height = Math.Max(MinHeight, mainWindowInfo.Height);
                    PixelPoint position = new PixelPoint((int)mainWindowInfo.Left, (int)mainWindowInfo.Top);

                    // skip if the saved spot is no longer on any monitor (e.g. a display was unplugged)
                    if (Screens.ScreenFromPoint(position) != null)
                    {
                        Position = position;
                    }
                    WindowState = mainWindowInfo.IsMaximized ? WindowState.Maximized : WindowState.Normal;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        public void SaveWindowState()
        {
            try
            {
                MainWindowInfo mainWindowInfo = new MainWindowInfo()
                {
                    Width = WidthNormal,
                    Height = HeightNormal,
                    Top = TopNormal,
                    Left = LeftNormal,
                    IsMaximized = WindowState == WindowState.Maximized
                };

                _runtimeDataService.RuntimeData.MainWindowInfo = mainWindowInfo;
                _runtimeDataService.Save();
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        #endregion Methods

    }
}