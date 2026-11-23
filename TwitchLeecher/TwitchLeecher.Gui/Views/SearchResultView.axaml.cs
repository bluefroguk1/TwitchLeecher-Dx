using Avalonia.Controls;
using Avalonia.Threading;

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

                // the first size is applied without animation; later resizes animate
                if (ToolbarGrid.Classes.Contains("settling"))
                {
                    Dispatcher.UIThread.Post(() => ToolbarGrid.Classes.Set("settling", false),
                        DispatcherPriority.Background);
                }
            };
        }

        #endregion Constructors
    }
}