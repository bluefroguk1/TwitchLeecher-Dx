using System;
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

        private HttpListener _httpListener;

        public AuthListener(IAuthService authService, IEventAggregator eventAggregator)
        {
            _authService = authService;
            _eventAggregator = eventAggregator;
        }

        public string StartListenForToken()
        {
            // Only one listener can own the port; a new request replaces the old one and its code.
            _httpListener?.Close();

            string code = Constants.NewOneTimeCode();
            var httpListener = new HttpListener();
            httpListener.Prefixes.Add(Constants.RedirectUrl + "/");
            httpListener.Start();
            _httpListener = httpListener;

            _ = Listen(httpListener, code);

            return code;
        }

        private async Task Listen(HttpListener httpListener, string code)
        {
            try
            {
                while (true)
                {
                    var context = await httpListener.GetContextAsync();
                    var query = context.Request.QueryString;

                    if (query.ContainsKey("access_token") || query.ContainsKey("sub_token"))
                    {
                        // Any page in the user's browser can call localhost, so only accept the token with our one-time code.
                        string receivedCode = query.ContainsKey("access_token") ? query["state"] : query["code"];

                        if (!string.Equals(receivedCode, code, StringComparison.Ordinal))
                        {
                            await Respond(context, 403, "This sign-in link has expired. Please start the sign-in again from Twitch Leecher.");
                            continue;
                        }

                        await Respond(context, 200, SignedInPage, "text/html; charset=utf-8");
                        httpListener.Stop();

                        if (query.ContainsKey("access_token"))
                        {
                            if (_authService.ValidateAuthentication(query["access_token"], false))
                            {
                                _eventAggregator.GetEvent<AuthResultEvent>().Publish(true);
                            }
                        }
                        else
                        {
                            // Validates, stores the token and publishes SubOnlyAuthChangedEvent.
                            _authService.ValidateAuthentication(query["sub_token"], true);
                        }

                        return;
                    }

                    // Twitch puts the token in the URL fragment, which the browser never sends; turn it into a query.
                    await Respond(context, 200,
                        "<script>let url = window.location.href.split(\"#\");window.location.href=url[0] + \"?\" + url[1]</script>", "text/html; charset=utf-8");
                }
            }
            catch (Exception) when (!httpListener.IsListening)
            {
                // Listener was stopped or replaced by a newer sign-in.
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

        private static async Task Respond(HttpListenerContext context, int statusCode, string body,
            string contentType = "text/plain; charset=utf-8")
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            var buffer = Encoding.UTF8.GetBytes(body);
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }
    }
}
