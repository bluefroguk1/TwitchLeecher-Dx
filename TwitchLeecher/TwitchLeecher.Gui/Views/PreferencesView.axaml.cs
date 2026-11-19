using Avalonia.Controls;
using Avalonia.Threading;
using TwitchLeecher.Gui.ViewModels;

namespace TwitchLeecher.Gui.Views
{
    public partial class PreferencesView : UserControl
    {
        public PreferencesView()
        {
            InitializeComponent();
            ScrollPositionRestorer.Attach(this, Scroller);

            // Downloads' "watched searches" pill opens Settings at that section. Background priority runs
            // after layout, so this wins over the restored scroll position.
            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is PreferencesViewModel { ScrollToWatchedSearches: true } vm)
                {
                    vm.ScrollToWatchedSearches = false;
                    Dispatcher.UIThread.Post(() => WatchedSearchesSection.BringIntoView(), DispatcherPriority.Background);
                }
            };
        }
    }
}
