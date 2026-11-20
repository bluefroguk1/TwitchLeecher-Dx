using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using TwitchLeecher.Core.Enums;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Types;
using TwitchLeecher.Shared.Commands;

namespace TwitchLeecher.Gui.ViewModels
{
    // Settings > Watched searches. Self-contained (own view, no Preferences clone): every change goes
    // straight to IAutoDownloadService, which saves it, so it is not part of the Settings Save/Undo.
    public class WatchedSearchesViewModel : ViewModelBase
    {
        #region Constants

        private const int ACTIVITY_ROWS = 8;

        #endregion Constants

        #region Fields

        private readonly IAutoDownloadService _autoDownloadService;

        private readonly DispatcherTimer _clockTimer;
        private readonly IDialogService _dialogService;

        private string _statusText;
        private List<WatchedActivity> _activity = new List<WatchedActivity>();
        private List<IntervalOption> _intervalOptions;

        #endregion Fields

        #region Constructors

        public WatchedSearchesViewModel(IAutoDownloadService autoDownloadService, IDialogService dialogService)
        {
            _autoDownloadService = autoDownloadService;
            _dialogService = dialogService;

            CheckNowCommand = new DelegateCommand(CheckNow);
            AddCommand = new DelegateCommand(Add);
            EditCommand = new DelegateCommand<WatchedSearch>(s => s.IsEditing = !s.IsEditing);
            RemoveCommand = new DelegateCommand<WatchedSearch>(ConfirmRemove);

            _autoDownloadService.Changed += (_, _) => Refresh();

            // "2 min ago · next in 13 min" moves on even when nothing else changes
            _clockTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background,
                (_, _) => UpdateStatusText());
            _clockTimer.Start();

            Refresh();
        }

        #endregion Constructors

        #region Properties

        public ObservableCollection<WatchedSearch> Searches => _autoDownloadService.Searches;

        public bool Enabled
        {
            get => _autoDownloadService.Enabled;
            set
            {
                _autoDownloadService.Enabled = value;
                FirePropertyChanged();
                UpdateStatusText();
            }
        }

        public List<IntervalOption> IntervalOptions
        {
            get => _intervalOptions;
            private set => SetProperty(ref _intervalOptions, value);
        }

        public IntervalOption SelectedInterval
        {
            get => IntervalOptions.FirstOrDefault(o => o.Minutes == _autoDownloadService.Interval);
            set
            {
                if (value != null)
                {
                    _autoDownloadService.Interval = value.Minutes;
                    UpdateStatusText();
                }
            }
        }

        // "Default quality", then the same list as Settings > Download.
        public List<string> QualityOptions { get; } =
            new[] { "Default quality" }.Concat(Preferences.DefaultQualities.Select(q => q.DisplayString)).ToList();

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public List<WatchedActivity> Activity
        {
            get => _activity;
            private set
            {
                SetProperty(ref _activity, value);
                FirePropertyChanged(nameof(HasActivity));
            }
        }

        public bool HasActivity => Activity.Count > 0;

        public bool HasSearches => Searches.Count > 0;

        public ICommand CheckNowCommand { get; }

        public ICommand AddCommand { get; }

        public ICommand EditCommand { get; }

        private async void ConfirmRemove(WatchedSearch search)
        {
            if (search == null)
            {
                return;
            }

            MessageBoxResult answer = await _dialogService.ShowMessageBox(
                "Stop watching " + search.Label + "?", "Remove watched search",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
            {
                Searches.Remove(search);
            }
        }

        public ICommand RemoveCommand { get; }

        #endregion Properties

        #region Methods

        private void CheckNow()
        {
            _autoDownloadService.CheckNow();
            StatusText = "Checking now…";
        }

        private void Add()
        {
            WatchedSearch search = _autoDownloadService.Add(string.Empty, VideoType.Broadcast);
            search.IsEditing = true;
        }

        private void Refresh()
        {
            BuildIntervalOptions();

            List<WatchedActivity> activity = _autoDownloadService.ReadActivity(200);
            Activity = activity.Take(ACTIVITY_ROWS).ToList();

            foreach (WatchedSearch search in Searches)
            {
                WatchedActivity latest = activity.FirstOrDefault(a =>
                    string.Equals(a.Search, search.Label, StringComparison.OrdinalIgnoreCase));

                // shortcut: matched by label, so renaming a channel loses its latest result until the next event; store the result per search if that matters
                search.LatestResult = !search.Active
                    ? "Search switched off"
                    : latest == null
                        ? "Nothing found yet"
                        : $"{Describe(latest.Kind)} {FormatWhen(latest.Time)} · {latest.Message}";
            }

            FirePropertyChanged(nameof(Enabled));
            FirePropertyChanged(nameof(HasSearches));
            UpdateStatusText();
        }

        private void BuildIntervalOptions()
        {
            List<int> minutes = new List<int> { 5, 15, 30, 60, 180, 360, 720, 1440 };

            // a migrated or hand-edited interval that is not in the list still shows
            if (!minutes.Contains(_autoDownloadService.Interval))
            {
                minutes.Add(_autoDownloadService.Interval);
                minutes.Sort();
            }

            if (IntervalOptions == null || IntervalOptions.Count != minutes.Count)
            {
                IntervalOptions = minutes.Select(m => new IntervalOption(m)).ToList();
            }

            FirePropertyChanged(nameof(SelectedInterval));
        }

        private void UpdateStatusText()
        {
            DateTime? last = _autoDownloadService.LastCheck;

            string text = last.HasValue ? "Last check " + FormatAgo(DateTime.Now - last.Value) : "Not checked yet";

            if (!_autoDownloadService.Enabled)
            {
                text += " · switched off";
            }
            else if (last.HasValue)
            {
                TimeSpan next = last.Value.AddMinutes(_autoDownloadService.Interval) - DateTime.Now;
                text += next.TotalMinutes < 1 ? " · next in under a minute" : " · next in " + FormatSpan(next);
            }

            StatusText = text;
        }

        private static string Describe(string kind)
        {
            switch (kind)
            {
                case "STARTED": return "Downloading";
                case "PAUSED": return "Waiting in Downloads";
                case "WAITING": return "Still live";
                case "SKIPPED": return "Skipped";
                case "FIRST RUN": return "First check";
                case "ERROR": return "Check failed";
                default: return kind;
            }
        }

        private static string FormatWhen(DateTime time)
        {
            return time.Date == DateTime.Today ? time.ToString("HH:mm") : time.ToString("d MMM HH:mm");
        }

        private static string FormatAgo(TimeSpan span)
        {
            return span.TotalMinutes < 1 ? "just now" : FormatSpan(span) + " ago";
        }

        private static string FormatSpan(TimeSpan span)
        {
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours} h {span.Minutes} min"
                : $"{Math.Max(1, (int)span.TotalMinutes)} min";
        }

        #endregion Methods

        #region Nested Types

        public class IntervalOption
        {
            public IntervalOption(int minutes)
            {
                Minutes = minutes;
            }

            public int Minutes { get; }

            public override string ToString()
            {
                return Minutes % 60 == 0 ? (Minutes == 60 ? "1 hour" : Minutes / 60 + " hours") : Minutes + " min";
            }
        }

        #endregion Nested Types
    }
}
