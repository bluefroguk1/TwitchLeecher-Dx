using System.Text.RegularExpressions;
using System.Web;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using TwitchLeecher.Core.Constants;
using TwitchLeecher.Gui.Types;

namespace TwitchLeecher.Gui.Views
{
    /// <summary>
    /// Twitch sign-in inside the app. Takes the OAuth access token from the redirect and, if asked,
    /// the website's "auth-token" cookie for sub-only downloads. Uses a private web session that is
    /// wiped when the window closes, so nothing stays signed in after sign-out.
    /// </summary>
    public partial class TwitchSignInWindow : Window
    {
        private const string AUTH_TOKEN_COOKIE = "auth-token";

        private static readonly Uri TwitchHome = new Uri("https://www.twitch.tv/");
        private static readonly Uri TwitchLogin = new Uri("https://www.twitch.tv/login");
        private static readonly Regex AuthTokenInCookieString = new Regex(@"(?:^|[;\s""])auth-token=([^;""\s]+)");

        private readonly string _state = Constants.NewOneTimeCode();
        private readonly bool _signIn;
        private readonly bool _subOnly;
        private readonly string _dataFolder;
        private readonly DispatcherTimer _cookieTimer;

        private bool _redirectHandled;
        private bool _visitedTwitchHome;
        private bool _checkingCookie;
        private bool _cookiesCleared;

        public TwitchSignInWindow()
        {
            InitializeComponent();
        }

        /// <param name="signIn">Run the app's OAuth sign-in.</param>
        /// <param name="subOnly">Also read the website token for sub-only downloads.</param>
        /// <param name="dataFolder">App data folder; WebView2 needs a writable folder.</param>
        public TwitchSignInWindow(bool signIn, bool subOnly, string dataFolder) : this()
        {
            _signIn = signIn;
            _subOnly = subOnly;
            _dataFolder = dataFolder;

            _cookieTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _cookieTimer.Tick += async (_, _) => await CheckSubOnlyCookie();

            WebView.EnvironmentRequested += OnEnvironmentRequested;
            WebView.NavigationStarted += (_, e) => e.Cancel = TryHandleRedirect(e.Request);
            WebView.NavigationCompleted += (_, e) => TryHandleRedirect(e.Request);
            Closing += OnClosing;

            if (signIn)
            {
                WebView.Source = new Uri(Constants.AuthorizeUrl(_state));
            }
            else
            {
                WebView.Source = TwitchLogin;
                _cookieTimer.Start();
            }
        }

        public TwitchSignInResult Result { get; } = new TwitchSignInResult();

        public static bool IsWebViewAvailable()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WebView2).IsInstalled;
                }

                if (OperatingSystem.IsLinux())
                {
                    return WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WebKitGtk).IsInstalled;
                }
            }
            catch
            {
                // Missing native libraries: use the browser fallback
            }

            return false;
        }

        private void OnEnvironmentRequested(object sender, WebViewEnvironmentRequestedEventArgs e)
        {
            switch (e)
            {
                case WindowsWebView2EnvironmentRequestedEventArgs webView2:
                    webView2.IsInPrivateModeEnabled = true;
                    webView2.UserDataFolder = Path.Combine(_dataFolder, "WebView2");
                    break;

                case LinuxWpeWebViewEnvironmentRequestedEventArgs wpe:
                    // IsWebViewAvailable checks WebKitGTK, so always use it.
                    wpe.PreferWebKitGtkInstead = true;
                    break;

                case GtkWebViewEnvironmentRequestedEventArgs gtk:
                    gtk.EphemeralDataManager = true;
                    break;
            }
        }

        // Returns true when the navigation is our OAuth redirect, which must not load.
        private bool TryHandleRedirect(Uri uri)
        {
            if (!_signIn || _redirectHandled || uri == null
                || !uri.GetLeftPart(UriPartial.Authority).Equals(Constants.RedirectUrl, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fragment = HttpUtility.ParseQueryString(uri.Fragment.TrimStart('#'));
            string accessToken = fragment["access_token"];
            bool denied = HttpUtility.ParseQueryString(uri.Query)["error"] != null;

            if (accessToken == null && !denied)
            {
                // No fragment on this event; NavigationCompleted gets another chance.
                return false;
            }

            _redirectHandled = true;

            if (accessToken != null && string.Equals(fragment["state"], _state, StringComparison.Ordinal))
            {
                Result.AccessToken = accessToken;

                if (_subOnly)
                {
                    Title = "Enabling sub-only downloads...";
                    _cookieTimer.Start();
                    return true;
                }
            }

            Dispatcher.UIThread.Post(() => Close());
            return true;
        }

        private async Task CheckSubOnlyCookie()
        {
            if (_checkingCookie || Result.SubOnlyToken != null)
            {
                return;
            }

            _checkingCookie = true;

            try
            {
                string token = await ReadAuthTokenCookie();

                if (token != null)
                {
                    Result.SubOnlyToken = token;
                    _cookieTimer.Stop();
                    Close();
                }
                else if (_signIn && !_visitedTwitchHome)
                {
                    // The OAuth login may not set the website cookie; www.twitch.tv sets it for the signed-in session.
                    _visitedTwitchHome = true;
                    Title = "Enabling sub-only downloads... (sign in to Twitch here if asked)";
                    WebView.Navigate(TwitchHome);
                }
            }
            catch
            {
                // Page or web view not ready yet; the timer tries again.
            }
            finally
            {
                _checkingCookie = false;
            }
        }

        private async Task<string> ReadAuthTokenCookie()
        {
            if (WebView.TryGetCookieManager() is { } cookieManager)
            {
                foreach (var cookie in await cookieManager.GetCookiesAsync())
                {
                    if (cookie.Name == AUTH_TOKEN_COOKIE && IsTwitchDomain(cookie.Domain) && !string.IsNullOrWhiteSpace(cookie.Value))
                    {
                        return cookie.Value;
                    }
                }

                return null;
            }

            // WebKitGTK has no cookie API in this package. auth-token is not HttpOnly, so a twitch.tv page can read it.
            if (WebView.Source == null || !IsTwitchDomain(WebView.Source.Host))
            {
                return null;
            }

            Match match = AuthTokenInCookieString.Match(await WebView.InvokeScript("document.cookie") ?? string.Empty);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static bool IsTwitchDomain(string domain)
        {
            return ("." + domain.TrimStart('.')).EndsWith(".twitch.tv", StringComparison.OrdinalIgnoreCase);
        }

        private async void OnClosing(object sender, WindowClosingEventArgs e)
        {
            _cookieTimer?.Stop();

            if (_cookiesCleared)
            {
                return;
            }

            // WebView2 keeps the private session alive for the whole app run, so delete its cookies before closing.
            e.Cancel = true;
            _cookiesCleared = true;

            try
            {
                if (WebView.TryGetCookieManager() is { } cookieManager)
                {
                    foreach (var cookie in await cookieManager.GetCookiesAsync())
                    {
                        cookieManager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path);
                    }
                }
            }
            catch
            {
                // Best effort: the session is private and ends when the app exits.
            }

            Close();
        }
    }
}
