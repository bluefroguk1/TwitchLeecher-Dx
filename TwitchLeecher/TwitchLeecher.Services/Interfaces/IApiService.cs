using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Services.Interfaces
{
    public interface IApiService
    {
        bool ValidateAuthentication(string accessToken, bool subOnly);

        void RevokeAuthentication(string accessToken, bool subOnly);

        TwitchVideoAuthInfo GetVodAuthInfo(string id);

        Dictionary<TwitchVideoQuality, string> GetPlaylistInfo(string vodId, TwitchVideoAuthInfo vodAuthInfo);

        Dictionary<TwitchVideoQuality, string> GetClipQualities(string slug);

        List<TwitchChatMessage> GetChatMessages(string vodId, TimeSpan from, TimeSpan to, Action<TimeSpan> onPage);

        bool ChannelExists(string channel);

        string GetChannelIdByName(string channel);

        // Helix users: display name and avatar URL, null when the channel is not found
        (string DisplayName, string ProfileImageUrl)? GetChannelInfo(string channel);

        ObservableCollection<TwitchVideo> Search(SearchParameters searchParams);
    }
}