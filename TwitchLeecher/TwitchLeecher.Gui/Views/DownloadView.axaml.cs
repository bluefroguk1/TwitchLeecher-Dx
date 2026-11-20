using Avalonia.Controls;
using System.Linq;
using Avalonia.Input;

namespace TwitchLeecher.Gui.Views
{
    public partial class DownloadView : UserControl
    {
        public DownloadView()
        {
            InitializeComponent();
        }

        // the From / To boxes take HH:MM:SS, so only digits and colons can be typed
        // shortcut: pasted text skips this filter and is checked when the field loses focus, as before
        private void TimeBox_TextInput(object sender, TextInputEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Text) && !e.Text.All(c => char.IsAsciiDigit(c) || c == ':'))
            {
                e.Handled = true;
            }
        }
    }
}