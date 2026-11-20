namespace TwitchLeecher.Services.Interfaces
{
    public interface IAuthListener
    {
        /// <summary>
        /// Fallback for systems without a web view: listens on localhost for a token.
        /// Returns a one-time code; a token is only accepted together with this code
        /// (as OAuth "state" or as "code" next to "sub_token").
        /// </summary>
        string StartListenForToken();
    }
}
