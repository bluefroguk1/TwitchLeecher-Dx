using Avalonia.Controls;

namespace TwitchLeecher.Gui.Views
{
    public partial class SearchResultView : UserControl
    {
        #region Constructors

        public SearchResultView() {
            InitializeComponent();
            ScrollPositionRestorer.Attach(this, Scroller);

            // The toolbar stays on one row and shrinks in stages as the pane narrows.
            SizeChanged += (_, e) =>
            {
                ToolbarGrid.Classes.Set("tight", e.NewSize.Width < 820);
                ToolbarGrid.Classes.Set("tiny", e.NewSize.Width < 620);
            };
        }

        #endregion Constructors
    }
}