using System;
using Microsoft.Extensions.DependencyInjection;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.ViewModels;
using TwitchLeecher.Gui.Views;

namespace TwitchLeecher.Gui.Services
{
    internal class NotificationService : INotificationService
    {
        #region Fields

        private readonly IServiceProvider _services;

        #endregion Fields

        #region Constructor

        public NotificationService(IServiceProvider services)
        {
            _services = services;
        }

        #endregion Constructor

        #region Methods

        public void ShowNotification(string text)
        {
            var mainWindow = _services.GetRequiredService<MainWindow>();
            var mainWindowViewModel = (MainWindowVM)mainWindow.DataContext;
            mainWindowViewModel.SetNotification(text);
        }

        #endregion Methods
    }
}