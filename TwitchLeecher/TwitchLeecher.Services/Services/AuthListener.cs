using System.Net;
using System.Text;
using System.Threading.Tasks;
using TwitchLeecher.Core.Constants;
using TwitchLeecher.Core.Events;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Events;
using TwitchLeecher.Shared.Extensions;

namespace TwitchLeecher.Services.Services
{
    internal class AuthListener : IAuthListener
    {
        private readonly IAuthService _authService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRuntimeDataService _runtimeDataService;

        public AuthListener(IAuthService authService, IEventAggregator eventAggregator,
            IRuntimeDataService runtimeDataService)
        {
            _authService = authService;
            _eventAggregator = eventAggregator;
            _runtimeDataService = runtimeDataService;
        }

        public async Task StartListenForToken()
        {
            var httpListener = new HttpListener();
            httpListener.Prefixes.Add(Constants.RedirectUrl + "/");
            httpListener.Start();
            while (true)
            {
                var context = await httpListener.GetContextAsync();
                if (context.Request.QueryString.ContainsKey("access_token"))
                {
                    var accessToken = context.Request.QueryString["access_token"];
                    await WriteSignedInPage(context.Response);
                    if (_authService.ValidateAuthentication(accessToken, false))
                    {
                        FireSubOnlyAuthenticationSuccess();
                    }

                    httpListener.Stop();
                    return;
                }

                if (context.Request.QueryString.ContainsKey("sub_token"))
                {
                    var accessToken = context.Request.QueryString["sub_token"];
                    await WriteSignedInPage(context.Response);
                    _runtimeDataService.RuntimeData.AuthInfo.AccessTokenSubOnly = accessToken;
                    _eventAggregator.GetEvent<SubOnlyAuthChangedEvent>().Publish(true);

                    httpListener.Stop();
                    return;
                }
                else
                {
                    context.Response.StatusCode = 200;
                    var buffer =
                        Encoding.UTF8.GetBytes(
                            "<script>let url = window.location.href.split(\"#\");window.location.href=url[0] + \"?\" + url[1]</script>");
                    await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    context.Response.Close();
                }
            }
        }

        // window.close() is usually blocked for tabs the app opened, so the page must read fine when it stays open
        private const string SignedInPage =
            "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Twitch Leecher</title>" +
            "<style>body{margin:0;height:100vh;display:flex;align-items:center;justify-content:center;" +
            "font-family:system-ui,-apple-system,'Segoe UI',sans-serif;background:#18181b;color:#efeff1}" +
            ".card{text-align:center;padding:40px 48px;border-radius:12px;background:#1f1f23;border-top:4px solid #9146FF}" +
            "h1{margin:0 0 8px;font-size:24px;color:#bf94ff}p{margin:0;color:#adadb8}</style></head>" +
            "<body><div class=\"card\"><h1>Signed in to Twitch Leecher</h1><p>You can close this tab</p></div>" +
            "<script>window.close()</script></body></html>";

        private static async Task WriteSignedInPage(HttpListenerResponse response)
        {
            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";
            var buffer = Encoding.UTF8.GetBytes(SignedInPage);
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        private void FireSubOnlyAuthenticationSuccess()
        {
            _eventAggregator.GetEvent<AuthResultEvent>().Publish(true);
        }
    }
}