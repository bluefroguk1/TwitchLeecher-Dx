using System;
using System.Threading;

namespace TwitchLeecher.Services.Services
{
    // Keeps the total download speed of all downloads together under a limit, by making each finished chunk wait
    // until the time it would have taken at that speed. It evens out over a few chunks, not within one.
    internal sealed class SpeedLimiter
    {
        private readonly object _lock = new object();
        private DateTime _free = DateTime.MinValue;

        public void Wait(long bytes, int megabytesPerSecond, CancellationToken cancellationToken)
        {
            if (megabytesPerSecond <= 0 || bytes <= 0)
            {
                return;
            }

            TimeSpan wait;

            lock (_lock)
            {
                DateTime now = DateTime.UtcNow;

                // time already spent idle doesn't count as credit for a burst
                if (_free < now)
                {
                    _free = now;
                }

                _free += TimeSpan.FromSeconds(bytes / (megabytesPerSecond * 1024.0 * 1024.0));
                wait = _free - now;
            }

            if (wait > TimeSpan.Zero)
            {
                cancellationToken.WaitHandle.WaitOne(wait);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
