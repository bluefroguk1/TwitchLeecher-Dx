using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TwitchLeecher.Gui.Views
{
    public partial class PreferencesView : UserControl
    {
        // until the box has had focus its caret sits at 0, and the end is the better guess
        private bool _fileNameBoxFocused;

        public PreferencesView()
        {
            InitializeComponent();
            FileNameBox.GotFocus += (_, _) => _fileNameBoxFocused = true;
        }

        // Puts the clicked chip's wildcard (its Tag) at the caret, replacing any selection.
        private void InsertWildcard(object sender, RoutedEventArgs e)
        {
            if (sender is not Control { Tag: string wildcard })
            {
                return;
            }

            string text = FileNameBox.Text ?? string.Empty;

            if (!_fileNameBoxFocused)
            {
                FileNameBox.SelectionStart = FileNameBox.SelectionEnd = text.Length;
            }

            int start = Math.Clamp(Math.Min(FileNameBox.SelectionStart, FileNameBox.SelectionEnd), 0, text.Length);
            int end = Math.Clamp(Math.Max(FileNameBox.SelectionStart, FileNameBox.SelectionEnd), 0, text.Length);

            FileNameBox.Text = text.Substring(0, start) + wildcard + text.Substring(end);
            FileNameBox.CaretIndex = start + wildcard.Length;
            FileNameBox.Focus();
        }
    }
}
