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
                : Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");

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

        // GPU encoders that passed a test encode, per codec ("h264"/"hevc"); null = none works. Found once per run.
        private static readonly Dictionary<string, string> _gpuEncoders = new Dictionary<string, string>();
        private static readonly object _gpuLock = new object();

        public string CompressVideo(Action<string> log, Action<string> setStatus, Action<double> setProgress,
            Action<bool> setIsIndeterminate, string file, TimeSpan length, CompressionLevel level,
            CompressionEncoder encoder, CancellationToken cancellationToken)
        {
            // Light keeps H.264 for players that can't do HEVC; the others use H.265, which is about half the size
            (string codec, string cpuEncoder, int quality) = level switch
            {
                CompressionLevel.Light => ("h264", "libx264", 23),
                CompressionLevel.Strong => ("hevc", "libx265", 30),
                _ => ("hevc", "libx265", 26)
            };

            setStatus("Compressing");
            setIsIndeterminate(true);
            setProgress(0);

            string gpuEncoder = null;

            if (encoder != CompressionEncoder.Cpu)
            {
                log(Environment.NewLine + Environment.NewLine + "Looking for a working GPU encoder...");
                gpuEncoder = FindGpuEncoder(codec);
                log(gpuEncoder == null ? " none found, using the CPU." : " using " + gpuEncoder + ".");
            }

            string tempFile = Path.Combine(Path.GetDirectoryName(file),
                Path.GetFileNameWithoutExtension(file) + ".compressing" + Path.GetExtension(file));

            log(Environment.NewLine + Environment.NewLine + "Compressing '" + file + "' (" + level + ", " +
                (gpuEncoder ?? cpuEncoder) + ")...");

            bool ok = gpuEncoder != null &&
                      RunCompress(log, setProgress, setIsIndeterminate, file, tempFile, gpuEncoder, quality, length,
                          cancellationToken);

            bool usedGpu = ok;

            if (!ok && gpuEncoder != null)
            {
                log(Environment.NewLine + "GPU encoding failed, trying again on the CPU...");
            }

            if (!ok)
            {
                ok = RunCompress(log, setProgress, setIsIndeterminate, file, tempFile, cpuEncoder, quality, length,
                    cancellationToken);
            }

            if (!ok)
            {
                FileSystem.DeleteFile(tempFile);
                log(Environment.NewLine + "Compression failed, kept the original video.");
                return "Compression failed, the original video was kept";
            }

            long before = new FileInfo(file).Length;
            long after = new FileInfo(tempFile).Length;

            // GPU encoders can miss their size target (their quality scales differ per vendor): try the CPU once
            if (after >= before && usedGpu)
            {
                FileSystem.DeleteFile(tempFile);
                log(Environment.NewLine + "GPU result was not smaller (" + FormatSize(before) + " -> " +
                    FormatSize(after) + "), trying again on the CPU...");

                if (RunCompress(log, setProgress, setIsIndeterminate, file, tempFile, cpuEncoder, quality, length,
                        cancellationToken))
                {
                    after = new FileInfo(tempFile).Length;
                }
                else
                {
                    FileSystem.DeleteFile(tempFile);
                    log(Environment.NewLine + "Compression failed, kept the original video.");
                    return "Compression failed, the original video was kept";
                }
            }

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

        // Video options per encoder at roughly comparable quality (lower number = better quality, bigger file).
        // shortcut: GPU quality numbers reuse the CPU CRF scale; tune per vendor if files come out too big or soft
        private static IEnumerable<string> VideoArgs(string encoder, int quality)
        {
            string q = quality.ToString(CultureInfo.InvariantCulture);

            if (encoder.EndsWith("_nvenc", StringComparison.Ordinal))
            {
                // NVENC's CQ scale is finer than x264/x265 CRF: the same number gave a file bigger than the Twitch
                // source (hevc cq 26 = 184 MB from 166 MB, where x265 crf 26 = about 50 MB). Measured: hevc cq = crf + 8
                // is about the same size as x265. h264 uses +5 (not measured, the fallback in CompressVideo covers it).
                string cq = (quality + (encoder.StartsWith("hevc", StringComparison.Ordinal) ? 8 : 5))
                    .ToString(CultureInfo.InvariantCulture);
                return new[] { "-c:v", encoder, "-preset", "p5", "-rc", "vbr", "-cq", cq, "-b:v", "0" };
            }

            if (encoder.EndsWith("_qsv", StringComparison.Ordinal))
            {
                return new[] { "-c:v", encoder, "-preset", "medium", "-global_quality", q };
            }

            if (encoder.EndsWith("_amf", StringComparison.Ordinal))
            {
                return new[] { "-c:v", encoder, "-quality", "quality", "-rc", "cqp", "-qp_i", q, "-qp_p", q, "-qp_b", q };
            }

            if (encoder.EndsWith("_videotoolbox", StringComparison.Ordinal))
            {
                // VideoToolbox has no CRF; map the 23..30 scale onto its 0..100 quality (higher = better)
                int vt = Math.Clamp(100 - (quality - 18) * 4, 30, 80);
                return new[] { "-c:v", encoder, "-q:v", vt.ToString(CultureInfo.InvariantCulture) };
            }

            return new[] { "-c:v", encoder, "-preset", "medium", "-crf", q };
        }

        // ffmpeg lists hardware encoders even without the hardware, so each candidate does a tiny test encode
        private string FindGpuEncoder(string codec)
        {
            lock (_gpuLock)
            {
                if (_gpuEncoders.TryGetValue(codec, out string known))
                {
                    return known;
                }

                // shortcut: no VAAPI (Linux) because it needs a device path; add -vaapi_device /dev/dri/renderD128 if Linux users ask
                string[] candidates = OperatingSystem.IsMacOS()
                    ? new[] { codec + "_videotoolbox" }
                    : new[] { codec + "_nvenc", codec + "_qsv", codec + "_amf" };

                string found = null;

                foreach (string candidate in candidates)
                {
                    if (TestEncoder(candidate))
                    {
                        found = candidate;
                        break;
                    }
                }

                _gpuEncoders[codec] = found;
                return found;
            }
        }

        private bool TestEncoder(string encoder)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(FFMPEGExe)
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                foreach (string arg in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i",
                             "color=c=black:s=256x144:d=1", "-frames:v", "5" })
                {
                    psi.ArgumentList.Add(arg);
                }

                foreach (string arg in VideoArgs(encoder, 26))
                {
                    psi.ArgumentList.Add(arg);
                }

                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add("null");
                psi.ArgumentList.Add("-");

                using (Process p = Process.Start(psi))
                {
                    p.StandardError.ReadToEnd();
                    p.StandardOutput.ReadToEnd();

                    if (!p.WaitForExit(15000))
                    {
                        try { p.Kill(true); } catch { }
                        return false;
                    }

                    return p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool RunCompress(Action<string> log, Action<double> setProgress, Action<bool> setIsIndeterminate,
            string file, string tempFile, string encoder, int quality, TimeSpan length,
            CancellationToken cancellationToken)
        {
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

            List<string> args = new List<string> { "-nostdin", "-y", "-i", file, "-map", "0:v:0?", "-map", "0:a?" };
            args.AddRange(VideoArgs(encoder, quality));
            args.AddRange(new[] { "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", tempFile });

            foreach (string arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

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

                if (p.ExitCode == 0 && File.Exists(tempFile))
                {
                    return true;
                }

                foreach (string line in logQueue)
                {
                    log(Environment.NewLine + line);
                }

                FileSystem.DeleteFile(tempFile);
                return false;
            }
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