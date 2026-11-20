namespace TwitchLeecher.Services.Interfaces
{
    // OS-level toast notifications; does nothing when the user turned them off or the OS backend is unavailable
    public interface IDesktopNotificationService
    {
        void Notify(string title, string message);
    }
}
