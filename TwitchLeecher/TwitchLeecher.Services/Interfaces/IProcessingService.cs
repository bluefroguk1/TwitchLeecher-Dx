using System;
using System.Collections.Generic;
using System.Threading;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Services.Interfaces
{
    public interface IProcessingService
    {
        string FFMPEGExe { get; }

        IEnumerable<string> ConcatParts(Action<string> log, Action<string> setStatus, Action<double> setProgress, TwitchPlaylist vodPlaylist, string concatFile);

        void ConvertVideo(Action<string> log, Action<string> setStatus, Action<double> setProgress, Action<bool> setIsIndeterminate, string sourceFile, string outputFile, CropInfo cropInfo);

        // Re-encodes a finished video to save space. Replaces the file only if the result is smaller;
        // returns a warning to show, or null.
        string CompressVideo(Action<string> log, Action<string> setStatus, Action<double> setProgress, Action<bool> setIsIndeterminate, string file, TimeSpan length, Core.Enums.CompressionLevel level, Core.Enums.CompressionEncoder encoder, CancellationToken cancellationToken);

        IEnumerable<string> ConvertParts(Action<string> log, Action<string> setStatus, Action<double> setProgress, Action<bool> setIsIndeterminate, TwitchPlaylist vodPlaylist, string outputFile, CropInfo cropInfo, CancellationToken cancellationToken);
    }
}