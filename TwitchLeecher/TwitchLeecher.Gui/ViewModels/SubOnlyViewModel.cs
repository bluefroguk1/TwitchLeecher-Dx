using System.Windows.Input;
using TwitchLeecher.Core.Events;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Types;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Commands;
using TwitchLeecher.Shared.Events;

namespace TwitchLeecher.Gui.ViewModels
{
    public class SubOnlyViewModel : ViewModelBase
    {
        private readonly IAuthListener _authListener;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly INavigationService _navigationService;

        private ICommand _connectCommand;
        private bool _isManualExpanded;
        private string _manualScript;

        public SubOnlyViewModel(IAuthListener authListener, IAuthService authService, IDialogService dialogService,
            IEventAggregator eventAggregator, INavigationService navigationService)
        {
            _authListener = authListener;
            _authService = authService;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _navigationService = navigationService;
        }

        public ICommand ConnectCommand => _connectCommand ??= new DelegateCommand(Connect);

        public bool IsManualExpanded
        {
            get => _isManualExpanded;
            set => SetProperty(ref _isManualExpanded, value);
        }

        public string ManualScript
        {
            get => _manualScript;
            private set => SetProperty(ref _manualScript, value);
        }

        public override void OnBeforeShown()
        {
            _eventAggregator.GetEvent<SubOnlyAuthChangedEvent>().Subscribe(Changed);

            // Manual method: the listener only accepts the token together with this one-time code.
            string code = _authListener.StartListenForToken();
            ManualScript = "let token = document.cookie.match('(^|;)\\\\s*auth-token\\\\s*=\\\\s*([^;]+)')?.pop() || ''; " +
                           $"window.location.href = 'http://localhost:9342?code={code}&sub_token=' + token;";

            base.OnBeforeShown();
        }

        public override void OnBeforeHidden()
        {
            _eventAggregator.GetEvent<SubOnlyAuthChangedEvent>().Unsubscribe(Changed);
            base.OnBeforeHidden();
        }

        private async void Connect()
        {
            try
            {
                TwitchSignInResult result = await _dialogService.ShowTwitchSignIn(false, true);

                if (result == null)
                {
                    IsManualExpanded = true;
                    await _dialogService.ShowMessageBox(
                        "No built-in web view is available on this system. Please use the manual method.",
                        "Sub-only", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validates, stores the token and publishes SubOnlyAuthChangedEvent, which closes this screen.
                if (result.SubOnlyToken != null && !_authService.ValidateAuthentication(result.SubOnlyToken, true))
                {
                    await _dialogService.ShowMessageBox("Twitch could not validate the sub-only token. Please try again.",
                        "Sub-only", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void Changed(bool obj)
        {
            _navigationService.NavigateBack();
        }
    }
}
