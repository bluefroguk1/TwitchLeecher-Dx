namespace TwitchLeecher.Gui.Interfaces
{
    public interface IAutoDownloadService
    {
        // Starts the background channel check loop. Safe to call more than once.
        void Start();
    }
}
