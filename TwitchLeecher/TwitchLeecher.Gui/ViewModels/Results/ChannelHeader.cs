using Avalonia.Media.Imaging;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Core;
using TwitchLeecher.Core;
using TwitchLeecher.Services.Interfaces;

namespace TwitchLeecher.Gui.ViewModels.Results
{
    // The channel header above the results: display name, avatar and the favourite star.
    // propertyChanged is called with the name of the view model property that changed.
    internal class ChannelHeader
    {
        public const string AvatarProperty = "ChannelAvatar";
        public const string TitleProperty = "SearchTitle";
        public const string IsChannelSearchProperty = "IsChannelSearch";
        public const string IsFavouriteProperty = "IsFavourite";

        private readonly IApiService _apiService;
        private readonly IPreferencesService _preferencesService;
        private readonly INotificationService _notificationService;
        private readonly IDialogService _dialogService;
        private readonly Action<string> _propertyChanged;
        private readonly object _lockObject = new object();

        // the search these results came from; SearchService resets its LastSearchParams when preferences are saved
        private SearchParameters _params;

        public ChannelHeader(IApiService apiService, IPreferencesService preferencesService,
            INotificationService notificationService, IDialogService dialogService, Action<string> propertyChanged)
        {
            _apiService = apiService;
            _preferencesService = preferencesService;
            _notificationService = notificationService;
            _dialogService = dialogService;
            _propertyChanged = propertyChanged;
        }

        public SearchParameters Params => _params;

        public string DisplayName { get; private set; }

        public Bitmap Avatar { get; private set; }

        public bool IsChannelSearch => _params.SearchType == SearchType.Channel;

        public bool IsFavourite =>
            IsChannelSearch
            && _preferencesService.CurrentPreferences.SearchFavouriteChannels.Any(c =>
                c.Equals(_params.Channel, StringComparison.OrdinalIgnoreCase));

        public async void Load(SearchParameters p)
        {
            _params = p;
            DisplayName = null;
            Avatar = null;
            _propertyChanged(AvatarProperty);
            _propertyChanged(IsChannelSearchProperty);
            _propertyChanged(IsFavouriteProperty);

            if (p.SearchType != SearchType.Channel || string.IsNullOrWhiteSpace(p.Channel))
            {
                return;
            }

            try
            {
                var info = await Task.Run(() => _apiService.GetChannelInfo(p.Channel));

                // a newer search may have finished meanwhile
                if (info == null || p != _params)
                {
                    return;
                }

                DisplayName = info.Value.DisplayName;
                _propertyChanged(TitleProperty);

                if (Uri.TryCreate(info.Value.ProfileImageUrl, UriKind.Absolute, out Uri avatarUrl))
                {
                    Bitmap avatar = await ImageHelper.LoadFromWeb(avatarUrl);

                    if (p == _params)
                    {
                        Avatar = avatar;
                        _propertyChanged(AvatarProperty);
                    }
                }
            }
            catch (Exception ex)
            {
                // the header is cosmetic, the results stay usable without it
                Console.WriteLine($"Could not load channel header for '{p.Channel}': {ex.Message}");
            }
        }

        public void ToggleFavourite()
        {
            try
            {
                lock (_lockObject)
                {
                    Preferences prefs = _preferencesService.CurrentPreferences;
                    string existing = prefs.SearchFavouriteChannels.FirstOrDefault(c =>
                        c.Equals(_params.Channel, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        prefs.SearchFavouriteChannels.Remove(existing);
                    }
                    else
                    {
                        prefs.SearchFavouriteChannels.Add(_params.Channel);
                    }

                    _preferencesService.Save(prefs);
                    _notificationService.ShowNotification(existing != null ? "Removed from favourites" : "Added to favourites");
                    _propertyChanged(IsFavouriteProperty);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }
    }
}
