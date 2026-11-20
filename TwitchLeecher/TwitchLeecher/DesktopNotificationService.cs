using System;
using System.Threading.Tasks;
using TwitchLeecher.Services.Interfaces;
#if WINDOWS
using DesktopNotifications.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
#else
using DesktopNotifications;
using DesktopNotifications.FreeDesktop;
#endif

namespace TwitchLeecher;

// Lives in the app project because the Windows backend needs the windows TFM, which only this project uses.
internal class DesktopNotificationService : IDesktopNotificationService
{
    public const string AppUserModelId = "TwitchLeecher.Dx";

    private readonly IPreferencesService _preferencesService;
    private readonly ILogService _logService;

#if WINDOWS
    private readonly ToastNotifier _notifier;
#else
    private readonly Lazy<Task<INotificationManager>> _manager;
#endif

    public DesktopNotificationService(IPreferencesService preferencesService, ILogService logService)
    {
        _preferencesService = preferencesService;
        _logService = logService;

#if WINDOWS
        // Resolved at start-up (App), so the process gets its AUMID before any window exists.
        try
        {
            // An unpackaged exe only gets toasts when a Start Menu shortcut carries its AUMID.
            // This (re)writes "%AppData%\Microsoft\Windows\Start Menu\Programs\Twitch Leecher DX.lnk" pointing at
            // this exe with AppUserModelID = AppUserModelId, and calls SetCurrentProcessExplicitAppUserModelID.
            WindowsApplicationContext.FromCurrentProcess("Twitch Leecher DX", AppUserModelId);

            // Our own notifier with that exact id: on this TFM the library's WindowsNotificationManager goes through
            // ToastNotificationManagerCompat, which doesn't use the AUMID above.
            _notifier = ToastNotificationManager.CreateToastNotifier(AppUserModelId);
        }
        catch (Exception ex)
        {
            // no toast support: carry on without notifications
            _logService.LogException(ex);
        }
#else
        _manager = new Lazy<Task<INotificationManager>>(CreateManager);
#endif
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
#if WINDOWS
            if (_notifier == null)
            {
                return;
            }

            XmlDocument xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            XmlNodeList texts = xml.GetElementsByTagName("text");
            texts[0].AppendChild(xml.CreateTextNode(title));
            texts[1].AppendChild(xml.CreateTextNode(message ?? string.Empty));
            _notifier.Show(new ToastNotification(xml));

            await Task.CompletedTask;
#else
            INotificationManager manager = await _manager.Value;

            if (manager != null)
            {
                await manager.ShowNotification(new Notification { Title = title, Body = message });
            }
#endif
        }
        catch (Exception ex)
        {
            // a failed toast must never affect downloads; logged so a Windows test can see why
            _logService.LogException(ex);
        }
    }

#if !WINDOWS
    private static async Task<INotificationManager> CreateManager()
    {
        try
        {
            // shortcut: Linux (D-Bus) only, macOS has no backend here and just gets no notifications
            INotificationManager manager = new FreeDesktopNotificationManager(FreeDesktopApplicationContext.FromCurrentProcess());
            await manager.Initialize();
            return manager;
        }
        catch
        {
            // no notification daemon: carry on without notifications
            return null;
        }
    }
#endif
}
