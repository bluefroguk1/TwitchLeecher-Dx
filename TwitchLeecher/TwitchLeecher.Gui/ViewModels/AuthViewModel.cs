using System.Diagnostics;
using System.Windows.Input;
using TwitchLeecher.Core.Constants;
using TwitchLeecher.Core.Events;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Types;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Commands;
using TwitchLeecher.Shared.Events;

namespace TwitchLeecher.Gui.ViewModels
{
    public class AuthViewModel : ViewModelBase
    {
        #region Fields

        private readonly IDialogService _dialogService;
        private readonly IAuthListener _authListener;
        private readonly IAuthService _authService;
        private readonly IEventAggregator _eventAggregator;

        private ICommand _connectCommand;
        private bool _enableSubOnly;

        #endregion Fields

        #region Constructor

        public AuthViewModel(
            IDialogService dialogService,
            IAuthListener authListener,
            IAuthService authService,
            IEventAggregator eventAggregator)
        {
            _dialogService = dialogService;
            _authListener = authListener;
            _authService = authService;
            _eventAggregator = eventAggregator;
        }

        #endregion Constructor

        #region Properties

        public ICommand ConnectCommand => _connectCommand ??= new DelegateCommand(Connect);

        public bool EnableSubOnly
        {
            get => _enableSubOnly;
            set => SetProperty(ref _enableSubOnly, value);
        }

        #endregion Properties

        #region Methods

        private async void Connect()
        {
            try
            {
                TwitchSignInResult result = await _dialogService.ShowTwitchSignIn(true, EnableSubOnly);

                if (result == null)
                {
                    ConnectInBrowser();
                    return;
                }

                if (result.AccessToken == null)
                {
                    // Window closed before signing in
                    return;
                }

                if (!_authService.ValidateAuthentication(result.AccessToken, false))
                {
                    await _dialogService.ShowMessageBox("Twitch could not validate the sign-in. Please try again.",
                        "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (EnableSubOnly && (result.SubOnlyToken == null || !_authService.ValidateAuthentication(result.SubOnlyToken, true)))
                {
                    await _dialogService.ShowMessageBox(
                        "You are signed in, but sub-only downloads could not be enabled. You can try again from the sub-only button at the top.",
                        "Sub-only", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _eventAggregator.GetEvent<AuthResultEvent>().Publish(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        // Fallback for systems without a web view: the user's browser redirects to the localhost listener.
        // Sub-only then stays a separate step on the sub-only screen.
        private void ConnectInBrowser()
        {
            string state = _authListener.StartListenForToken();
            Process.Start(new ProcessStartInfo(Constants.AuthorizeUrl(state))
            {
                UseShellExecute = true,
                Verb = "open"
            });
        }

        #endregion Methods
    }
}
