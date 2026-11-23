using System.Collections.ObjectModel;
using System.ComponentModel;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Gui.Interfaces
{
    public interface ISearchService : INotifyPropertyChanged
    {
        ObservableCollection<TwitchVideo> Videos { get; }

        SearchParameters LastSearchParams { get; }

        // true while a search is running; false and HasSearched = true once its results are in Videos
        bool IsSearching { get; }

        bool HasSearched { get; }

        void PerformSearch(SearchParameters searchParams);
    }
}

