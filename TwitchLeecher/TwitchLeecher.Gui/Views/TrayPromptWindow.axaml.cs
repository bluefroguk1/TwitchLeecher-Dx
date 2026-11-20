using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TwitchLeecher.Gui.Views;

// Asked once, the first time the main window is minimised.
public partial class TrayPromptWindow : Window
{
    public TrayPromptWindow()
    {
        InitializeComponent();
    }

    // null when closed with the window's X button (no answer)
    public bool? UseTray { get; private set; }

    public bool DontAskAgainChecked => DontAskAgain.IsChecked == true;

    private void Yes_OnClick(object sender, RoutedEventArgs e)
    {
        UseTray = true;
        Close();
    }

    private void No_OnClick(object sender, RoutedEventArgs e)
    {
        UseTray = false;
        Close();
    }
}
