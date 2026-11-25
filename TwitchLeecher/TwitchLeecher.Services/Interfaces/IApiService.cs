using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Services.Interfaces
{
    public interface IApiService
    {
        bool ValidateAuthentication(string accessToken, bool subOnly);

        // Twitch login of the last main token that validated, for "Signed in as"
        string SignedInLogin { get; }

        // true = Twitch accepts the token, false = Twitch rejected it (expired, revoked, password changed),
        // null = could not tell (offline, Twitch down). Unlike ValidateAuthentication it changes nothing.
        bool? CheckToken(string accessToken, bool subOnly);

        void RevokeAuthentication(string accessToken, bool subOnly);

        TwitchVideoAuthInfo GetVodAuthInfo(string id);

        Dictionary<TwitchVideoQuality, string> GetPlaylistInfo(string vodId, TwitchVideoAuthInfo vodAuthInfo);

        Dictionary<TwitchVideoQuality, string> GetClipQualities(string slug);

        TwitchStoryboard GetStoryboard(string vodId);

        // the stream's game changes; empty when it stayed on one game
        List<(TimeSpan Start, string Game)> GetChapters(string vodId);

        List<TwitchChatMessage> GetChatMessages(string vodId, TimeSpan from, TimeSpan to, Action<TimeSpan> onPage);

        bool ChannelExists(string channel);

        // When the channel is streaming now: the UTC start of that stream; null when it is offline.
        // Throws on network or API errors so callers can play safe.
        DateTime? GetLiveStreamStart(string channel);

        string GetChannelIdByName(string channel);

        // Helix users: display name and avatar URL, null when the channel is not found
        (string DisplayName, string ProfileImageUrl)? GetChannelInfo(string channel);

        ObservableCollection<TwitchVideo> Search(SearchParameters searchParams);
    }
}