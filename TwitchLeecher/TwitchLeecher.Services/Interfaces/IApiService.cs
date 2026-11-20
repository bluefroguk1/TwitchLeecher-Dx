using System.Collections.Generic;
using System.Collections.ObjectModel;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Services.Interfaces
{
    public interface IApiService
    {
        bool ValidateAuthentication(string accessToken, bool subOnly);

        // true = Twitch accepts the token, false = Twitch rejected it (expired, revoked, password changed),
        // null = could not tell (offline, Twitch down). Unlike ValidateAuthentication it changes nothing.
        bool? CheckToken(string accessToken, bool subOnly);

        void RevokeAuthentication(string accessToken, bool subOnly);

        TwitchVideoAuthInfo GetVodAuthInfo(string id);

        Dictionary<TwitchVideoQuality, string> GetPlaylistInfo(string vodId, TwitchVideoAuthInfo vodAuthInfo);

        bool ChannelExists(string channel);

        string GetChannelIdByName(string channel);

        ObservableCollection<TwitchVideo> Search(SearchParameters searchParams);
    }
}