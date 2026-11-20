using Ninject.Modules;
using TwitchLeecher.Gui.Interfaces;
using TwitchLeecher.Gui.Services;
using TwitchLeecher.Gui.ViewModels;

namespace TwitchLeecher.Gui.Modules
{
    public class GuiModule : NinjectModule
    {
        #region Methods

        public override void Load()
        {
            Bind<IAutoDownloadService>().To<AutoDownloadService>().InSingletonScope();
            Bind<IScheduleService>().To<ScheduleService>().InSingletonScope();
            Bind<IDialogService>().To<DialogService>().InSingletonScope();
            Bind<IDonationService>().To<DonationService>().InSingletonScope();
            Bind<INavigationService>().To<NavigationService>().InSingletonScope();
            Bind<INotificationService>().To<NotificationService>().InSingletonScope();
            Bind<ISearchService>().To<SearchService>().InSingletonScope();
            // one instance, shown in the right-hand pane of the Search page (keeps sort, selection and scroll)
            Bind<SearchResultViewModel>().ToSelf().InSingletonScope();
        }

        #endregion Methods
    }
}