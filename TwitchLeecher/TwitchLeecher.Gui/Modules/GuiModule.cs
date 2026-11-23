using Microsoft.Extensions.DependencyInjection;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Services;
using TwitchLeecher.Gui.ViewModels;

namespace TwitchLeecher.Gui.Modules
{
    public static class GuiModule
    {
        #region Methods

        public static void Load(IServiceCollection services)
        {
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IDonationService, DonationService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<ISearchService, SearchService>();

            // pages and dialogs: a new instance each time, as before
            services.AddTransient<AuthViewModel>();
            services.AddTransient<DownloadViewModel>();
            services.AddTransient<DownloadsViewModel>();
            services.AddTransient<InfoViewModel>();
            services.AddTransient<LoadingViewModel>();
            services.AddTransient<LogViewModel>();
            services.AddTransient<PreferencesViewModel>();
            services.AddTransient<SearchResultViewModel>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<SubOnlyViewModel>();
            services.AddTransient<UpdateInfoViewModel>();
            services.AddTransient<WelcomeViewModel>();
        }

        #endregion Methods
    }
}