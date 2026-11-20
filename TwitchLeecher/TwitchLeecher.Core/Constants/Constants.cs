namespace TwitchLeecher.Core.Constants
{
    public static class Constants
    {
        public const string ClientId = "9yb8jxpbo20072j3lawklcudlico4u";

        public const string ClientIdWeb = "kimne78kx3ncx6brgo4mv6wki5h1ko";

        public const string RedirectUrl = "http://localhost:9342";

        // Shared by the running instance (listener) and a second launch (sender).
        public const string PipeName = "TwitchLeecher-DX";

        // state is a one-time random value, checked when Twitch redirects back.
        public static string AuthorizeUrl(string state) =>
            $"https://id.twitch.tv/oauth2/authorize?client_id={ClientId}&redirect_uri={RedirectUrl}&response_type=token&scope=user:read:subscriptions user_subscriptions&force_verify=true&state={state}";

        public static string NewOneTimeCode() =>
            System.Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
    }
}