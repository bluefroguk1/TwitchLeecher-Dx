using Microsoft.Extensions.DependencyInjection;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Services.Services;

namespace TwitchLeecher.Services.Modules
{
    public static class ServiceModule
    {
        #region Methods

        public static void Load(IServiceCollection services)
        {
            services.AddSingleton<IApiService, ApiService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IDownloadService, DownloadService>();
            services.AddSingleton<IFilenameService, FilenameService>();
            services.AddSingleton<IFolderService, FolderService>();
            services.AddSingleton<ILogService, LogService>();
            services.AddSingleton<IPreferencesService, PreferencesService>();
            services.AddSingleton<IProcessingService, ProcessingService>();
            services.AddSingleton<IRuntimeDataService, RuntimeDataService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IAuthListener, AuthListener>();
            services.AddSingleton<IThemeService, ThemeService>();
        }

        #endregion Methods
    }
}