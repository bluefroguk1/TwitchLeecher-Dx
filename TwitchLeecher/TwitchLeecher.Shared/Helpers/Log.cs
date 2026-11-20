using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TwitchLeecher.Shared.Reflection;

namespace TwitchLeecher.Shared.Helpers
{
    /// <summary>
    /// Records exceptions that are swallowed on purpose, so the cause is not lost.
    /// Never throws. Writes to Debug output and to a small rolling file
    /// (%APPDATA%/&lt;product&gt;/logs/swallowed.log).
    /// </summary>
    public static class Log
    {
        private const string LOGS_FOLDER_NAME = "logs";
        private const string LOG_FILE_NAME = "swallowed.log";
        private const long MAX_FILE_BYTES = 512 * 1024;
        private static readonly TimeSpan _repeatWindow = TimeSpan.FromSeconds(60);

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, DateTime> _lastLogged = new Dictionary<string, DateTime>();
        private static string _logFile;

        public static void Swallowed(string context, Exception ex)
        {
            try
            {
                string line = context + ": " + (ex == null ? "(no exception)" : ex.GetType().Name + ": " + ex.Message);
                DateTime now = DateTime.UtcNow;

                lock (_lock)
                {
                    // Rate-limit identical messages so a failing hot loop cannot flood the file
                    if (_lastLogged.TryGetValue(line, out DateTime last) && now - last < _repeatWindow)
                    {
                        return;
                    }

                    // shortcut: key set only ever grows by distinct messages, reset if it gets large
                    if (_lastLogged.Count > 500)
                    {
                        _lastLogged.Clear();
                    }

                    _lastLogged[line] = now;

                    Debug.WriteLine("[swallowed] " + line);

                    string file = GetLogFile();
                    Directory.CreateDirectory(Path.GetDirectoryName(file));

                    FileInfo info = new FileInfo(file);

                    if (info.Exists && info.Length > MAX_FILE_BYTES)
                    {
                        string old = file + ".old";
                        File.Delete(old);
                        File.Move(file, old);
                    }

                    File.AppendAllText(file, now.ToString("u") + " " + line + Environment.NewLine +
                        (ex == null ? string.Empty : ex.ToString() + Environment.NewLine));
                }
            }
            catch
            {
                // Logging must never crash or alter the calling code path
            }
        }

        private static string GetLogFile()
        {
            if (_logFile == null)
            {
                string productName = AssemblyUtil.Get.GetProductName();
                _logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    productName, LOGS_FOLDER_NAME, LOG_FILE_NAME);
            }

            return _logFile;
        }
    }
}
