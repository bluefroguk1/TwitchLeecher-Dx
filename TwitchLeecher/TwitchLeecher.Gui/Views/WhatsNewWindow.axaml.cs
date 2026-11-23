using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TwitchLeecher.Gui.Views;

// "What's new" card, shown once when an existing install first runs this release.
public partial class WhatsNewWindow : Window
{
    // The release the text in WhatsNewWindow.axaml describes. Change both together for the next feature release.
    public static readonly Version ForVersion = new Version(4, 0);

    private const string RELEASES_URL = "https://github.com/schneidermanuel/TwitchLeecher-Dx/releases/tag/v";

    private readonly Version _running;

    public WhatsNewWindow() : this(ForVersion)
    {
    }

    public WhatsNewWindow(Version running)
    {
        _running = running;
        InitializeComponent();
        Heading.Text = "What's new in " + running.Major + "." + running.Minor;
    }

    // lastRun is the version that last saved the runtime data. A fresh install has none, so it reads as the
    // running version and the card is skipped; an older install reads lower and sees it once.
    public static bool ShouldShow(Version lastRun, Version running)
    {
        return lastRun < ForVersion && running >= ForVersion;
    }

    private void ReleaseNotes_OnClick(object sender, RoutedEventArgs e)
    {
        string url = RELEASES_URL + _running.Major + "." + _running.Minor + "." + Math.Max(_running.Build, 0);

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // no browser to open: the card stays, nothing is lost
        }
    }

    private void GotIt_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
