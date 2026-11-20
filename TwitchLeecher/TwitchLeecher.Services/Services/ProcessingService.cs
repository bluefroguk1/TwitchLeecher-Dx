using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Helpers;
using TwitchLeecher.Shared.IO;

namespace TwitchLeecher.Services.Services
{
    internal class ProcessingService : IProcessingService
    {
        #region Constants

        public string FFMPEGExe =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "ffmpeg"
                : Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ffmpeg.exe");

        #endregion Constants

        #region Properties

        #endregion Properties

        #region Methods

        public IEnumerable<string> ConcatParts(Action<string> log, Action<string> setStatus, Action<double> setProgress,
            TwitchPlaylist vodPlaylist, string concatFile)
        {
            var warnings = new List<string>();
            
            setStatus("Merging files");
            setProgress(0);

            log(Environment.NewLine + Environment.NewLine + "Merging all VOD parts into '" + concatFile + "'...");

            using (FileStream outputStream = new FileStream(concatFile, FileMode.OpenOrCreate, FileAccess.Write))
            {
                TwitchPlaylistPart initSegment = vodPlaylist.InitSegment;

                if (initSegment != null)
                {
                    if (File.Exists(initSegment.LocalFile))
                    {
                        using (FileStream initStream = new FileStream(initSegment.LocalFile, FileMode.Open, FileAccess.Read))
                        {
                            initStream.CopyTo(outputStream);
                        }

                        FileSystem.DeleteFile(initSegment.LocalFile);
                    }
                    else
                    {
                        warnings.Add($"Warning: missing VOD init segment: {initSegment.LocalFile}");
                    }
                }

                int partsCount = vodPlaylist.Count;

                for (int i = 0; i < partsCount; i++)
                {
                    TwitchPlaylistPart part = vodPlaylist[i];

                    if (!File.Exists(part.LocalFile)) {
                        warnings.Add($"Warning: missing VOD segment: {part.LocalFile}");
                        continue;
                    }

                    using (FileStream partStream = new FileStream(part.LocalFile, FileMode.Open, FileAccess.Read))
                    {
                        partStream.CopyTo(outputStream);
                    }

                    FileSystem.DeleteFile(part.LocalFile);

                    setProgress(i * 100 / partsCount);
                }
            }

            setProgress(100);
            return warnings;
        }


        public void ConvertVideo(Action<string> log, Action<string> setStatus, Action<double> setProgress,
            Action<bool> setIsIndeterminate, string sourceFile, string outputFile, CropInfo cropInfo)
        {
            RunFfmpeg(log, setStatus, setProgress, setIsIndeterminate, sourceFile, outputFile, cropInfo, null);
        }

        public IEnumerable<string> ConvertParts(Action<string> log, Action<string> setStatus,
            Action<double> setProgress, Action<bool> setIsIndeterminate, TwitchPlaylist vodPlaylist,
            string outputFile, CropInfo cropInfo, CancellationToken cancellationToken)
        {
            // Streams init segment + chunks (the exact bytes ConcatParts would write) into ffmpeg's stdin, so no
            // merged intermediate file is needed. stdin is not seekable, so the input-side -ss used for CropStart
            // cannot work here; callers must use ConcatParts + ConvertVideo when CropStart is set.
            if (cropInfo.CropStart)
            {
                throw new InvalidOperationException("Streaming conversion does not support CropStart.");
            }

            var warnings = new List<string>();

            RunFfmpeg(log, setStatus, setProgress, setIsIndeterminate, null, outputFile, cropInfo, stdin =>
            {
                TwitchPlaylistPart initSegment = vodPlaylist.InitSegment;

                if (initSegment != null)
                {
                    if (File.Exists(initSegment.LocalFile))
                    {
                        if (!PipeFile(initSegment.LocalFile, stdin))
                        {
                            return;
                        }
                    }
                    else
                    {
                        warnings.Add($"Warning: missing VOD init segment: {initSegment.LocalFile}");
                    }
                }

                int partsCount = vodPlaylist.Count;

                for (int i = 0; i < partsCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    TwitchPlaylistPart part = vodPlaylist[i];

                    if (!File.Exists(part.LocalFile))
                    {
                        warnings.Add($"Warning: missing VOD segment: {part.LocalFile}");
                        continue;
                    }

                    if (!PipeFile(part.LocalFile, stdin))
                    {
                        // ffmpeg stopped reading (e.g. reached the -t limit); remaining chunks are not needed
                        return;
                    }
                }
            }, cancellationToken);

            return warnings;
        }

        // Copies a chunk into ffmpeg's stdin and deletes it. Returns false if ffmpeg closed its input early.
        private static bool PipeFile(string file, Stream stdin)
        {
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[81920];
                int read;

                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    try
                    {
                        stdin.Write(buffer, 0, read);
                    }
                    catch (IOException)
                    {
                        return false;
                    }
                }
            }

            FileSystem.DeleteFile(file);
            return true;
        }

        public string CompressVideo(Action<string> log, Action<string> setStatus, Action<double> setProgress,
            Action<bool> setIsIndeterminate, string file, TimeSpan length, CompressionLevel level,
            CancellationToken cancellationToken)
        {
            // Light keeps H.264 for players that can't do HEVC; the others use H.265, which is about half the size
            (string codec, string crf) = level switch
            {
                CompressionLevel.Light => ("libx264", "23"),
                CompressionLevel.Strong => ("libx265", "30"),
                _ => ("libx265", "26")
            };

            string tempFile = Path.Combine(Path.GetDirectoryName(file),
                Path.GetFileNameWithoutExtension(file) + ".compressing" + Path.GetExtension(file));

            setStatus("Compressing");
            setIsIndeterminate(true);
            setProgress(0);
            log(Environment.NewLine + Environment.NewLine + "Compressing '" + file + "' (" + level + ")...");

            ProcessStartInfo psi = new ProcessStartInfo(FFMPEGExe)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (string arg in new[]
                     {
                         "-nostdin", "-y", "-i", file, "-map", "0:v:0?", "-map", "0:a?",
                         "-c:v", codec, "-preset", "medium", "-crf", crf,
                         "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", tempFile
                     })
            {
                psi.ArgumentList.Add(arg);
            }

            // shortcut: CPU encoding only (slow for long VODs), add hevc_nvenc/qsv/amf detection if users find it too slow
            log(Environment.NewLine + "Command line arguments: " + string.Join(" ", psi.ArgumentList) + Environment.NewLine);

            FixedSizeQueue<string> logQueue = new FixedSizeQueue<string>(200);

            using (Process p = new Process())
            {
                DataReceivedEventHandler onData = (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(e.Data))
                    {
                        return;
                    }

                    string line = e.Data.Trim();
                    logQueue.Enqueue(line);

                    TimeSpan? at = ParseFfmpegTime(line);

                    if (at.HasValue && length > TimeSpan.Zero)
                    {
                        setIsIndeterminate(false);
                        setProgress(Math.Min(100, at.Value.TotalMilliseconds / length.TotalMilliseconds * 100));
                    }
                };

                p.OutputDataReceived += onData;
                p.ErrorDataReceived += onData;
                p.StartInfo = psi;
                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();

                // poll so a cancel stops the (long) encode straight away
                while (!p.WaitForExit(500))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try { p.Kill(true); } catch { }
                        p.WaitForExit();
                        FileSystem.DeleteFile(tempFile);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

                p.WaitForExit();

                if (p.ExitCode != 0 || !File.Exists(tempFile))
                {
                    foreach (string line in logQueue)
                    {
                        log(Environment.NewLine + line);
                    }

                    FileSystem.DeleteFile(tempFile);
                    log(Environment.NewLine + "Compression failed, kept the original video.");
                    return "Compression failed, the original video was kept";
                }
            }

            long before = new FileInfo(file).Length;
            long after = new FileInfo(tempFile).Length;

            if (after >= before)
            {
                FileSystem.DeleteFile(tempFile);
                log(Environment.NewLine + "Compressed file was not smaller (" + FormatSize(before) + " -> " +
                    FormatSize(after) + "), kept the original.");
                return null;
            }

            File.Move(tempFile, file, true);
            log(Environment.NewLine + "Compressed " + FormatSize(before) + " -> " + FormatSize(after) + ".");
            setProgress(100);
            return null;
        }

        // ffmpeg progress lines look like "frame=  123 fps=... time=00:01:02.34 bitrate=..."
        private static TimeSpan? ParseFfmpegTime(string line)
        {
            int i = line.IndexOf("time=", StringComparison.Ordinal);

            if (i < 0)
            {
                return null;
            }

            string value = line.Substring(i + 5).Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out TimeSpan t) ? t : null;
        }

        private static string FormatSize(long bytes)
        {
            return bytes >= 1L << 30
                ? (bytes / (double)(1L << 30)).ToString("0.00", CultureInfo.CurrentCulture) + " GB"
                : (bytes / (double)(1L << 20)).ToString("0", CultureInfo.CurrentCulture) + " MB";
        }

        private void RunFfmpeg(Action<string> log, Action<string> setStatus, Action<double> setProgress,
            Action<bool> setIsIndeterminate, string sourceFile, string outputFile, CropInfo cropInfo,
            Action<Stream> feedStdin, CancellationToken cancellationToken = default)
        {
            bool piped = feedStdin != null;

            setStatus("Converting Video");
            setIsIndeterminate(true);

            log(Environment.NewLine + Environment.NewLine + "Executing '" + FFMPEGExe + "' on '" +
                (piped ? "VOD parts (streamed)" : sourceFile) + "'...");

            ProcessStartInfo psi = new ProcessStartInfo(FFMPEGExe)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // ffmpeg reads stdin for interactive commands; inherited garbage there corrupted conversions (#92)
            psi.ArgumentList.Add("-nostdin");
            psi.ArgumentList.Add("-y");

            if (cropInfo.CropStart)
            {
                psi.ArgumentList.Add("-ss");
                psi.ArgumentList.Add(cropInfo.Start.ToString(CultureInfo.InvariantCulture));
            }

            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(piped ? "pipe:0" : sourceFile);
            psi.ArgumentList.Add("-analyzeduration");
            psi.ArgumentList.Add(int.MaxValue.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("-probesize");
            psi.ArgumentList.Add(int.MaxValue.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("copy");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("copy");

            if (cropInfo.CropEnd)
            {
                psi.ArgumentList.Add("-t");
                psi.ArgumentList.Add(cropInfo.Length.ToString(CultureInfo.InvariantCulture));
            }

            psi.ArgumentList.Add(outputFile);

            log(Environment.NewLine + "Command line arguments: " + string.Join(" ", psi.ArgumentList) + Environment.NewLine);

            using (Process p = new Process())
            {
                FixedSizeQueue<string> logQueue = new FixedSizeQueue<string>(200);

                TimeSpan duration = TimeSpan.FromSeconds(cropInfo.Length);

                DataReceivedEventHandler outputDataReceived = new DataReceivedEventHandler((s, e) =>
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            string dataTrimmed = e.Data.Trim();

                            logQueue.Enqueue(dataTrimmed);

                            if (dataTrimmed.StartsWith("frame", StringComparison.OrdinalIgnoreCase) &&
                                duration != TimeSpan.Zero)
                            {
                                string timeStr = dataTrimmed.Substring(dataTrimmed.IndexOf("time") + 4).Trim();
                                timeStr = timeStr.Substring(timeStr.IndexOf("=") + 1).Trim();
                                timeStr = timeStr.Substring(0, timeStr.IndexOf(" ")).Trim();

                                if (TimeSpan.TryParse(timeStr, out TimeSpan current))
                                {
                                    setIsIndeterminate(false);
                                    setProgress(current.TotalMilliseconds / duration.TotalMilliseconds * 100);
                                }
                                else
                                {
                                    setIsIndeterminate(true);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log(Environment.NewLine + "An error occured while reading '" + FFMPEGExe + "' output stream!" +
                            Environment.NewLine + Environment.NewLine + ex.ToString());
                    }
                });

                p.OutputDataReceived += outputDataReceived;
                p.ErrorDataReceived += outputDataReceived;
                p.StartInfo = psi;
                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();

                if (piped)
                {
                    try
                    {
                        feedStdin(p.StandardInput.BaseStream);
                        p.StandardInput.BaseStream.Close();
                    }
                    catch
                    {
                        // canceled or failed while feeding: stop ffmpeg and drop the partial output
                        try { if (!p.HasExited) p.Kill(true); } catch { }
                        p.WaitForExit();
                        try { if (File.Exists(outputFile)) File.Delete(outputFile); } catch { }
                        throw;
                    }
                }

                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    log(Environment.NewLine + "Video conversion complete!");
                }
                else
                {
                    if (!logQueue.IsEmpty)
                    {
                        foreach (string line in logQueue)
                        {
                            log(Environment.NewLine + line);
                        }
                    }

                    throw new ApplicationException("An error occured while converting the video!");
                }
            }
        }

        #endregion Methods
    }
}