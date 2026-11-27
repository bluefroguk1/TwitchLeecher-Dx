using System;
using System.IO;
using Microsoft.Win32;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher;

// Registers the app to start when the user signs in (per user, no admin rights).
internal static class AutoStart
{
    public const string MinimizedArg = "--minimized";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "TwitchLeecher";

    // Called at start-up (the exe may have moved) and after every preferences save.
    public static void Apply(Preferences preferences)
    {
        string command = preferences.AppStartWithOs ? GetCommand(preferences.AppStartMinimised) : null;

        if (OperatingSystem.IsWindows())
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);

            if (command == null)
            {
                key.DeleteValue(RunValue, false);
            }
            else
            {
                key.SetValue(RunValue, command);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            // ApplicationData is $XDG_CONFIG_HOME or ~/.config on Linux
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "autostart", "twitchleecher.desktop");

            if (command == null)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file,
                    "[Desktop Entry]\n" +
                    "Type=Application\n" +
                    "Name=Twitch Leecher DX\n" +
                    // shortcut: no escaping of " ` $ \ in the path, fine for normal install paths; escape per the desktop entry spec if one shows up
                    $"Exec={command}\n" +
                    "X-GNOME-Autostart-enabled=true\n");
            }
        }
    }

    private static string GetCommand(bool minimised)
    {
        string exe = Environment.ProcessPath;
        string command = $"\"{exe}\"";

        // started as "dotnet TwitchLeecher.dll": autostart must pass the dll too
        if (string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            command += $" \"{Path.Combine(AppContext.BaseDirectory, "TwitchLeecher.dll")}\"";
        }

        return minimised ? command + " " + MinimizedArg : command;
    }
}
