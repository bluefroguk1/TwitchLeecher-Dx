using System;
using System.Threading.Tasks;
using DesktopNotifications;
using TwitchLeecher.Services.Interfaces;
#if WINDOWS
using DesktopNotifications.Windows;
#else
using DesktopNotifications.FreeDesktop;
#endif

namespace TwitchLeecher;

// Lives in the app project because the Windows backend needs the windows TFM, which only this project uses.
internal class DesktopNotificationService : IDesktopNotificationService
{
    private readonly IPreferencesService _preferencesService;
    private readonly Lazy<Task<INotificationManager>> _manager;

    public DesktopNotificationService(IPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
        _manager = new Lazy<Task<INotificationManager>>(CreateManager);
    }

    public void Notify(string title, string message)
    {
        if (!_preferencesService.CurrentPreferences.AppShowNotifications)
        {
            return;
        }

        _ = ShowAsync(title, message);
    }

    private async Task ShowAsync(string title, string message)
    {
        try
        {
            INotificationManager manager = await _manager.Value;

            if (manager != null)
            {
                await manager.ShowNotification(new Notification { Title = title, Body = message });
            }
        }
        catch
        {
            // a failed toast must never affect downloads
        }
    }

    private static async Task<INotificationManager> CreateManager()
    {
        try
        {
#if WINDOWS
            INotificationManager manager = new WindowsNotificationManager(WindowsApplicationContext.FromCurrentProcess());
#else
            // shortcut: Linux (D-Bus) only, macOS has no backend here and just gets no notifications
            INotificationManager manager = new FreeDesktopNotificationManager(FreeDesktopApplicationContext.FromCurrentProcess());
#endif
            await manager.Initialize();
            return manager;
        }
        catch
        {
            // no notification daemon / toast support: carry on without notifications
            return null;
        }
    }
}
