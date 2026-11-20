namespace TwitchLeecher.Gui.Interfaces
{
    // Starts held/paused downloads when the off-peak window opens and (optionally) pauses them when it closes.
    public interface IScheduleService
    {
        // idempotent
        void Start();
    }
}
