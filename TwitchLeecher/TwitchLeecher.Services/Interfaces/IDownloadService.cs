using System.Collections.ObjectModel;
using System.ComponentModel;
using TwitchLeecher.Core.Models;

namespace TwitchLeecher.Services.Interfaces
{
    public interface IDownloadService : INotifyPropertyChanged
    {
        #region Properties

        ObservableCollection<TwitchVideoDownload> Downloads { get; }

        #endregion Properties

        #region Methods

        void Enqueue(DownloadParameters downloadParams);

        /// <summary>startPaused: the item waits in the list until Resume(id) is called.</summary>
        void Enqueue(DownloadParameters downloadParams, bool startPaused);

        void Cancel(string id);

        void Retry(string id);

        void Remove(string id);

        /// <summary>Pauses every new download start (the whole queue).</summary>
        void Pause();

        void Resume();

        /// <summary>Pauses one item. A running download stops after its in-flight chunks and keeps the finished ones.</summary>
        void Pause(string id);

        void Resume(string id);

        void MoveUp(string id);

        /// <summary>Removes done, failed and canceled items.</summary>
        void ClearFinished();

        bool CanShutdown();

        void Shutdown();

        bool IsFileNameUsed(string fullPath);

        #endregion Methods
    }
}