using System;
using Microsoft.Extensions.DependencyInjection;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Events;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.ViewModels;
using TwitchLeecher.Shared.Events;

namespace TwitchLeecher.Gui.Services
{
    internal class NavigationService : INavigationService
    {
        #region Fields

        private readonly IServiceProvider _services;
        private readonly IEventAggregator _eventAggregator;

        private ViewModelBase _lastView;
        private ViewModelBase _currentView;

        private readonly Dictionary<Type, ViewModelBase> _persistentViews;

        #endregion Fields

        #region Constructor

        public NavigationService(IServiceProvider services, IEventAggregator eventAggregator)
        {
            _services = services;
            _eventAggregator = eventAggregator;

            _persistentViews = new Dictionary<Type, ViewModelBase>();
        }

        #endregion Constructor

        #region Methods

        public void ShowAuth()
        {
            Navigate(_services.GetRequiredService<AuthViewModel>());
        }

        public void ShowLoading()
        {
            Navigate(_services.GetRequiredService<LoadingViewModel>());
        }

        public void ShowSearch()
        {
            // already there (a search started from the form keeps the sidebar): don't build a second form
            if (_currentView is SearchViewModel)
            {
                return;
            }

            Navigate(_services.GetRequiredService<SearchViewModel>());
        }

        // results are shown in the right-hand pane of the Search page
        public void ShowSearchResults()
        {
            ShowSearch();
        }

        public void ShowDownload(DownloadParameters downloadParams)
        {
            DownloadViewModel model = _services.GetRequiredService<DownloadViewModel>();
            model.DownloadParams = downloadParams ?? throw new ArgumentNullException(nameof(downloadParams));

            Navigate(model);
        }

        public void ShowDownloads()
        {
            if (!_persistentViews.TryGetValue(typeof(DownloadsViewModel), out ViewModelBase vm))
            {
                vm = _services.GetRequiredService<DownloadsViewModel>();
                _persistentViews.Add(typeof(DownloadsViewModel), vm);
            }

            Navigate(vm);
        }

        public void ShowPreferences()
        {
            Navigate(GetPreferences());
        }

        public void ShowWatchedSearches()
        {
            PreferencesViewModel vm = GetPreferences();
            vm.ScrollToWatchedSearches = true;
            Navigate(vm);
        }

        private PreferencesViewModel GetPreferences()
        {
            if (!_persistentViews.TryGetValue(typeof(PreferencesViewModel), out ViewModelBase vm))
            {
                vm = _services.GetRequiredService<PreferencesViewModel>();
                _persistentViews.Add(typeof(PreferencesViewModel), vm);
            }

            return (PreferencesViewModel)vm;
        }

        public void ShowInfo()
        {
            Navigate(_services.GetRequiredService<InfoViewModel>());
        }

        public void ShowLog(TwitchVideoDownload download)
        {
            LogViewModel model = _services.GetRequiredService<LogViewModel>();
            model.Download = download ?? throw new ArgumentNullException(nameof(download));

            Navigate(model);
        }

        public void NavigateBack()
        {
            if (_lastView != null)
            {
                Navigate(_lastView);
            }
        }

        public void ShowAuthSubOnly()
        {
            Navigate(_services.GetRequiredService<SubOnlyViewModel>());
        }

        private void Navigate(ViewModelBase nextView)
        {
            if (nextView == null || (_currentView != null && _currentView.GetType() == nextView.GetType()))
            {
                return;
            }

            _currentView?.OnBeforeHidden();

            nextView.OnBeforeShown();

            _lastView = _currentView;

            _currentView = nextView;

            _eventAggregator.GetEvent<ShowViewEvent>().Publish(nextView);
        }

        #endregion Methods
    }
}