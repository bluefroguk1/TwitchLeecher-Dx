namespace TwitchLeecher.Services.Interfaces
{
    public interface IAuthService
    {
        bool IsAuthenticatedSubOnly { get; }

        // Twitch login of the signed-in account, or null
        string UserName { get; }

        bool ValidateAuthentication(string accessToken, bool subOnly);

        void RevokeAuthentication();
    }
}