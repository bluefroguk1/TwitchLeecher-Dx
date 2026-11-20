using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Types;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Services.Services;
using TwitchLeecher.Shared.Commands;

namespace TwitchLeecher.Gui.ViewModels
{
    public partial class PreferencesViewModel : ViewModelBase
    {
        #region Fields

        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly IPreferencesService _preferencesService;
        private readonly IThemeService _themeService;
        private readonly IFilenameService _filenameService;

        // what the file name preview is rendered with
        private static readonly TwitchVideo SampleVideo = new TwitchVideo("somestreamer", "Speedrun practice",
            "2290012345", 0, new TimeSpan(3, 0, 0), new DateTime(2026, 11, 16, 19, 30, 5), null, null, true, false,
            false);

        private static readonly TwitchVideoQuality SampleQuality =
            new TwitchVideoQuality("chunked", "Source", "1920x1080");

        private Preferences _currentPreferences;

        private ICommand _addFavouriteChannelCommand;
        private ICommand _removeFavouriteChannelCommand;
        private ICommand _chooseDownloadTempFolderCommand;
        private ICommand _chooseDownloadFolderCommand;
        private ICommand _chooseExternalPlayerCommand;
        private ICommand _clearExternalPlayerCommand;
        private ICommand _saveCommand;
        private ICommand _undoCommand;
        private ICommand _defaultsCommand;

        private readonly object _commandLockObject;
        private bool _isChannelDropDownOpen;
        private bool _hasChanges;
        private string _validationMessage;
        private int _selectedSectionIndex;

        #endregion Fields

        #region Constructors

        public PreferencesViewModel(
            IDialogService dialogService,
            INotificationService notificationService,
            IPreferencesService preferencesService,
            IThemeService themeService,
            IFilenameService filenameService)
        {
            _dialogService = dialogService;
            _notificationService = notificationService;
            _preferencesService = preferencesService;
            _themeService = themeService;
            _filenameService = filenameService;

            _commandLockObject = new object();
        }

        #endregion Constructors

        #region Properties
        
        public Preferences CurrentPreferences
        {
            get
            {
                if (_currentPreferences == null)
                {
                    _currentPreferences = Track(_preferencesService.CurrentPreferences.Clone());
                }

                return _currentPreferences;
            }

            // null reloads the saved preferences on next read, so there is nothing unsaved any more
            private set
            {
                SetProperty(ref _currentPreferences, value);
                HasChanges = false;
                ValidationMessage = null;
                FirePropertyChanged(nameof(FilenamePreview));
            }
        }

        // true only while the edited copy differs from the saved preferences, so values a control writes back
        // while the page loads (or a value changed back by hand) do not show the save bar
        public bool HasChanges
        {
            get => _hasChanges;
            private set => SetProperty(ref _hasChanges, value);
        }

        // shown in the save bar, because the field with the error may be in another section
        public string ValidationMessage
        {
            get => _validationMessage;
            private set => SetProperty(ref _validationMessage, value);
        }

        // kept here so the page reopens on the section it was left on
        public int SelectedSectionIndex
        {
            get => _selectedSectionIndex;
            set => SetProperty(ref _selectedSectionIndex, value);
        }

        public string FilenamePreview
        {
            get
            {
                try
                {
                    string filename = _filenameService.SubstituteWildcards(CurrentPreferences.DownloadFileName,
                        SampleVideo, SampleQuality, new TimeSpan(0, 12, 0), new TimeSpan(3, 0, 0));

                    return _filenameService.EnsureExtension(filename, CurrentPreferences.DownloadDisableConversion);
                }
                catch (Exception)
                {
                    // a preview must never break the page
                    return string.Empty;
                }
            }
        }

        public IEnumerable<string> AvailableThemes => _preferencesService.AvailableThemes;

        public ICommand AddFavouriteChannelCommand
        {
            get
            {
                if (_addFavouriteChannelCommand == null)
                {
                    _addFavouriteChannelCommand = new DelegateCommand(AddFavouriteChannel);
                }

                return _addFavouriteChannelCommand;
            }
        }

        public ICommand RemoveFavouriteChannelCommand
        {
            get
            {
                if (_removeFavouriteChannelCommand == null)
                {
                    _removeFavouriteChannelCommand = new DelegateCommand(RemoveFavouriteChannel);
                }

                return _removeFavouriteChannelCommand;
            }
        }

        public ICommand ChooseDownloadTempFolderCommand
        {
            get
            {
                if (_chooseDownloadTempFolderCommand == null)
                {
                    _chooseDownloadTempFolderCommand = new DelegateCommand(ChooseDownloadTempFolder);
                }

                return _chooseDownloadTempFolderCommand;
            }
        }

        public ICommand ChooseDownloadFolderCommand
        {
            get
            {
                if (_chooseDownloadFolderCommand == null)
                {
                    _chooseDownloadFolderCommand = new DelegateCommand(ChooseDownloadFolder);
                }

                return _chooseDownloadFolderCommand;
            }
        }

        public ICommand ChooseExternalPlayerCommand
        {
            get
            {
                if (_chooseExternalPlayerCommand == null)
                {
                    _chooseExternalPlayerCommand = new DelegateCommand(ChooseExternalPlayer);
                }

                return _chooseExternalPlayerCommand;
            }
        }

        public ICommand ClearExternalPlayerCommand
        {
            get
            {
                if (_clearExternalPlayerCommand == null)
                {
                    _clearExternalPlayerCommand = new DelegateCommand(ClearExternalPlayer);
                }

                return _clearExternalPlayerCommand;
            }
        }

        public ICommand SaveCommand
        {
            get
            {
                if (_saveCommand == null)
                {
                    _saveCommand = new DelegateCommand(Save);
                }

                return _saveCommand;
            }
        }

        public ICommand UndoCommand
        {
            get
            {
                if (_undoCommand == null)
                {
                    _undoCommand = ReactiveCommand.CreateFromTask(async () => Undo());
                }

                return _undoCommand;
            }
        }

        public ICommand DefaultsCommand
        {
            get
            {
                if (_defaultsCommand == null)
                {
                    _defaultsCommand = ReactiveCommand.CreateFromTask(async () => Defaults());
                }

                return _defaultsCommand;
            }
        }

        #endregion Properties

        #region Methods

        private void AddFavouriteChannel()
        {
            try
            {
                lock (_commandLockObject)
                {
                    string currentChannel = CurrentPreferences.SearchChannelName;

                    if (!string.IsNullOrWhiteSpace(currentChannel))
                    {
                        string existingEntry = CurrentPreferences.SearchFavouriteChannels.FirstOrDefault(channel =>
                            channel.Equals(currentChannel, StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrWhiteSpace(existingEntry))
                        {
                            CurrentPreferences.SearchChannelName = existingEntry;
                        }
                        else
                        {
                            CurrentPreferences.SearchFavouriteChannels.Add(currentChannel);
                        }

                        _notificationService.ShowNotification($"'{currentChannel}' added!");
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void RemoveFavouriteChannel()
        {
            try
            {
                lock (_commandLockObject)
                {
                    string currentChannel = CurrentPreferences.SearchChannelName;

                    if (!string.IsNullOrWhiteSpace(currentChannel))
                    {
                        string existingEntry = CurrentPreferences.SearchFavouriteChannels.FirstOrDefault(channel =>
                            channel.Equals(currentChannel, StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrWhiteSpace(existingEntry))
                        {
                            CurrentPreferences.SearchFavouriteChannels.Remove(existingEntry);
                            CurrentPreferences.SearchChannelName =
                                CurrentPreferences.SearchFavouriteChannels.FirstOrDefault();
                            _notificationService.ShowNotification($"'{currentChannel}' removed!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ChooseDownloadTempFolder()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _dialogService.ShowFolderBrowserDialog(CurrentPreferences.DownloadTempFolder,
                        ChooseDownloadTempFolderCallback);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ChooseDownloadTempFolderCallback(bool cancelled, string folder)
        {
            try
            {
                if (!cancelled)
                {
                    CurrentPreferences.DownloadTempFolder = folder;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ChooseDownloadFolder()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _dialogService.ShowFolderBrowserDialog(CurrentPreferences.DownloadFolder,
                        ChooseDownloadFolderCallback);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ChooseDownloadFolderCallback(bool cancelled, string folder)
        {
            try
            {
                if (!cancelled)
                {
                    CurrentPreferences.DownloadFolder = folder;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ChooseExternalPlayer()
        {
            try
            {
                lock (_commandLockObject)
                {
                    var filter = new CommonFileDialogFilter("exe", "Executables");
                    _dialogService.ShowFileBrowserDialog(filter, CurrentPreferences.MiscExternalPlayer,
                        ChooseExternalPlayerCallback);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ClearExternalPlayer()
        {
            try
            {
                lock (_commandLockObject)
                {
                    CurrentPreferences.MiscExternalPlayer = null;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void ChooseExternalPlayerCallback(bool cancelled, string file)
        {
            try
            {
                if (!cancelled)
                {
                    CurrentPreferences.MiscExternalPlayer = file;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private void Save()
        {
            try
            {
                lock (_commandLockObject)
                {
                    _dialogService.SetBusy();
                    ValidateForSaveBar();

                    if (!HasErrors)
                    {
                        _preferencesService.Save(_currentPreferences);
                        _themeService.SetTheme(_currentPreferences.Theme);
                        CurrentPreferences = null;
                        _notificationService.ShowNotification("Preferences saved");
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private async Task Undo()
        {
            try
            {
                MessageBoxResult result = await _dialogService.ShowMessageBox(
                    "Undo current changes and reload last saved preferences?", "Undo", MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _dialogService.SetBusy();
                    CurrentPreferences = null;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private async Task Defaults()
        {
            try
            {
                MessageBoxResult result = await _dialogService.ShowMessageBox("Load default preferences?", "Defaults",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // loaded as unsaved changes: the save bar's Save keeps them, Undo drops them
                    CurrentPreferences = Track(_preferencesService.CreateDefault());
                    HasChanges = true;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        public override void Validate(string propertyName = null)
        {
            base.Validate(propertyName);

            string currentProperty = nameof(CurrentPreferences);

            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == currentProperty)
            {
                CurrentPreferences?.Validate();

                if (CurrentPreferences.HasErrors)
                {
                    AddError(currentProperty, "Invalid Preferences!");
                }
            }
        }

        public override void OnBeforeHidden()
        {
            try
            {
                CurrentPreferences = null;
            }
            catch (Exception ex)
            {
                _dialogService.ShowAndLogException(ex);
            }
        }

        private Preferences Track(Preferences preferences)
        {
            preferences.PropertyChanged += PreferencesEdited;
            preferences.SearchFavouriteChannels.CollectionChanged += FavouritesEdited;
            return preferences;
        }

        private void FavouritesEdited(object sender, NotifyCollectionChangedEventArgs e)
        {
            PreferencesEdited(sender, null);
        }

        private void PreferencesEdited(object sender, PropertyChangedEventArgs e)
        {
            // edits to a copy that was already swapped out are not ours any more
            if (!ReferenceEquals(sender, _currentPreferences) &&
                !ReferenceEquals(sender, _currentPreferences?.SearchFavouriteChannels))
            {
                return;
            }

            HasChanges = !SameValues(_currentPreferences, _preferencesService.CurrentPreferences);
            FirePropertyChanged(nameof(FilenamePreview));

            // once a save failed, keep the message in step with the fixes
            if (ValidationMessage != null)
            {
                ValidateForSaveBar();
            }
        }

        // Compares the settings themselves (not validation state from the base class); null and "" count as equal.
        private static bool SameValues(Preferences edited, Preferences saved)
        {
            if (edited == null || saved == null)
            {
                return ReferenceEquals(edited, saved);
            }

            foreach (PropertyInfo property in typeof(Preferences).GetProperties(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object a = property.GetValue(edited);
                object b = property.GetValue(saved);

                if (a is string || b is string)
                {
                    if (!string.Equals(a as string ?? string.Empty, b as string ?? string.Empty))
                    {
                        return false;
                    }
                }
                else if (a is IEnumerable ea && b is IEnumerable eb)
                {
                    if (!ea.Cast<object>().SequenceEqual(eb.Cast<object>()))
                    {
                        return false;
                    }
                }
                else if (!Equals(a, b))
                {
                    return false;
                }
            }

            return true;
        }

        private void ValidateForSaveBar()
        {
            Validate();
            ValidationMessage = HasErrors
                ? string.Join(" ", CurrentPreferences.GetErrors().Cast<string>())
                : null;
        }

        public bool IsChannelDropDownOpen
        {
            get => _isChannelDropDownOpen;
            set => SetProperty(ref _isChannelDropDownOpen, value);
        }

        [RelayCommand]
        private void OpenDropdown()
        {
            CurrentPreferences.SearchChannelName = string.Empty;
            IsChannelDropDownOpen = true;
        }

        #endregion Methods
    }
}