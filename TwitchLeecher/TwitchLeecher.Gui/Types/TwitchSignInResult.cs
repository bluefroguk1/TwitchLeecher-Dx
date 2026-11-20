namespace TwitchLeecher.Gui.Types
{
    public class TwitchSignInResult
    {
        // null when the user closed the window before Twitch sent it
        public string AccessToken { get; set; }

        // Twitch website "auth-token" cookie, used for sub-only VODs
        public string SubOnlyToken { get; set; }
    }
}
