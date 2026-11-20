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

                        await Respond(context, 200, "Token received, you can close this window!");
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
                        "<script>let url = window.location.href.split(\"#\");window.location.href=url[0] + \"?\" + url[1]</script>");
                }
            }
            catch (Exception) when (!httpListener.IsListening)
            {
                // Listener was stopped or replaced by a newer sign-in.
            }
        }

        private static async Task Respond(HttpListenerContext context, int statusCode, string body)
        {
            context.Response.StatusCode = statusCode;
            var buffer = Encoding.UTF8.GetBytes(body);
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }
    }
}
