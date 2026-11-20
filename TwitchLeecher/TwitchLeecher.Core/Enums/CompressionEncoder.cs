namespace TwitchLeecher.Core.Enums
{
    // Which encoder "Compress after download" uses
    public enum CompressionEncoder
    {
        // the graphics card when one works, otherwise the CPU
        Auto,
        // smallest files, slowest, busy CPU
        Cpu,
        // fastest, files a bit bigger; falls back to the CPU when no GPU encoder works
        Gpu
    }
}
