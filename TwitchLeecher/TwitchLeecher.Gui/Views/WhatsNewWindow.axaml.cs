using System;
using System.Diagnostics;
using Avalonia;
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
        // placed by hand: with SizeToContent the built-in CenterOwner works from the size before layout
        Opened += (_, _) =>
        {
            CenterOnOwner();

            // the main window may still be settling when the card opens (restored from the tray, first layout), and the
            // user can move or resize it while the card is up: keep the card centred on it
            if (Owner is Window owner)
            {
                owner.SizeChanged += OwnerChanged;
                owner.PositionChanged += OwnerMoved;
            }
        };
        SizeChanged += (_, _) => CenterOnOwner();
        Closed += (_, _) =>
        {
            if (Owner is Window owner)
            {
                owner.SizeChanged -= OwnerChanged;
                owner.PositionChanged -= OwnerMoved;
            }
        };
    }

    private void OwnerChanged(object sender, SizeChangedEventArgs e) => CenterOnOwner();

    private void OwnerMoved(object sender, PixelPointEventArgs e) => CenterOnOwner();

    // Centre the card on the owner's client area, again whenever its own size changes (SizeToContent settles after opening).
    private void CenterOnOwner()
    {
        if (Owner is not Window owner)
        {
            return;
        }

        PixelPoint ownerTopLeft = owner.PointToScreen(new Point(0, 0));
        double ownerWidth = owner.ClientSize.Width * owner.RenderScaling;
        double ownerHeight = owner.ClientSize.Height * owner.RenderScaling;
        double width = ClientSize.Width * RenderScaling;
        double height = ClientSize.Height * RenderScaling;

        Position = new PixelPoint(
            (int)(ownerTopLeft.X + (ownerWidth - width) / 2),
            (int)(ownerTopLeft.Y + (ownerHeight - height) / 2));
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
