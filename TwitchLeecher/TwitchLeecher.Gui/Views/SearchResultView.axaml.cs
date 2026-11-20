using Avalonia.Controls;

namespace TwitchLeecher.Gui.Views
{
    public partial class SearchResultView : UserControl
    {
        #region Constructors

        public SearchResultView() {
            InitializeComponent();
            ScrollPositionRestorer.Attach(this, Scroller);

            // below this width the title and the controls no longer fit on one line
            SizeChanged += (_, e) =>
            {
                ToolbarGrid.Classes.Set("compact", e.NewSize.Width < 860);
                // and below this one even the second line is too short for the labels
                ToolbarGrid.Classes.Set("tight", e.NewSize.Width < 640);
            };
        }

        #endregion Constructors
    }
}