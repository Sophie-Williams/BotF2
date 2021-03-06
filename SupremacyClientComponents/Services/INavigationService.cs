using Microsoft.Practices.Composite.Logging;
using Microsoft.Practices.Composite.Regions;
using Supremacy.Annotations;
using Supremacy.Client.Commands;
using Supremacy.Client.Context;
using Supremacy.Client.Dialogs;
using Supremacy.Client.Events;
using Supremacy.Client.Input;
using Supremacy.Economy;
using Supremacy.Game;
using Supremacy.Resources;
using Supremacy.Universe;
using System;

namespace Supremacy.Client.Services
{
    public interface INavigationService
    {
        bool ActivateScreen(string screenName); // jumps to a different screen
        void NavigateToColony(Colony colony);   // jumps to the galaxy screen, selecting and centering the colony
        void RushColonyProduction(Colony colony);    // not the good place for this but don't know where to put it. Will ask the user if he really wants to rush the production
    }

    public class NavigationService : INavigationService
    {
        private readonly IDispatcherService _dispatcherService;
        private readonly IRegionManager _regionManager;
        private readonly INavigationCommandsProxy _navigationCommands;
        private readonly ILoggerFacade _logger;
        private readonly IAppContext _appContext;

        public NavigationService(
            [NotNull] IDispatcherService dispatcherService,
            [NotNull] IRegionManager regionManager,
            [NotNull] INavigationCommandsProxy navigationCommands,
            [NotNull] ILoggerFacade logger,
            [NotNull] IAppContext appContext)
        {
            if (dispatcherService == null)
                throw new ArgumentNullException("dispatcherService");
            if (regionManager == null)
                throw new ArgumentNullException("regionManager");
            if (navigationCommands == null)
                throw new ArgumentNullException("navigationCommands");
            if (logger == null)
                throw new ArgumentNullException("logger");
            if (appContext == null)
                throw new ArgumentNullException("appContext");

            _dispatcherService = dispatcherService;
            _regionManager = regionManager;
            _navigationCommands = navigationCommands;
            _logger = logger;
            _appContext = appContext;

            _navigationCommands.ActivateScreen.RegisterCommand(new DelegateCommand<string>(s => _dispatcherService.Invoke((Func<string, bool>)ActivateScreen, s)));
            _navigationCommands.NavigateToColony.RegisterCommand(new DelegateCommand<Colony>(NavigateToColony));
            _navigationCommands.RushColonyProduction.RegisterCommand(new DelegateCommand<Colony>(RushColonyProduction));
        }

        #region Implementation of INavigationService

        public bool ActivateScreen(string screenName)
        {
            var view = _regionManager.Regions[ClientRegions.GameScreens].GetView(screenName);
            if (view == null)
                return false;

            var activatingArgs = new ViewActivatingEventArgs(view);

            ClientEvents.ViewActivating.Publish(activatingArgs);

            if (activatingArgs.Cancel)
                return false;

            _logger.Log(
                string.Format("[INavigationService] Activating Screen: {0}", screenName),
                Category.Debug,
                Priority.None);

            _regionManager.Regions[ClientRegions.GameScreens].Activate(view);

            _logger.Log(
                string.Format("[INavigationService] Screen Activated: {0}", screenName),
                Category.Debug,
                Priority.None);

            return true;
        }

        public void NavigateToColony(Colony colony)
        {
            if (!_appContext.IsGameInPlay)
                return;

            var playerEmpire = _appContext.LocalPlayerEmpire;
            if (playerEmpire == null)
                return;

            if (colony == null)
                return;

            _logger.Log(
                string.Format("[INavigationService] Navigating to Colony: {0}", colony.Name),
                Category.Debug,
                Priority.None);

            var ownedByPlayer = (colony.OwnerID == playerEmpire.CivilizationID);

            ActivateScreen(StandardGameScreens.GalaxyScreen);

            GalaxyScreenCommands.SelectSector.Execute(colony.Sector);
            GalaxyScreenCommands.CenterOnSector.Execute(colony.Sector);

            if (!ownedByPlayer)
                return;
        }

        public void RushColonyProduction(Colony colony)
        {
            var playerEmpire = _appContext.LocalPlayerEmpire;
            if (playerEmpire == null)
                return;

            if (colony == null)
                return;

            var ownedByPlayer = (colony.OwnerID == playerEmpire.CivilizationID);
            if (!ownedByPlayer)
                return;

            BuildProject project = colony.BuildSlots[0].Project;
            if (project == null)
                return;

            if (project.IsCancelled || project.IsCompleted || project.IsRushed)
                return;

            int creditsNeeded = project.GetTotalCreditsCost();

            if (playerEmpire.Credits.CurrentValue < creditsNeeded)
                return;

            string confirmationMessage = string.Format(ResourceManager.GetString("CONFIRM_RUSH_BUILDING_MESSAGE"),
                creditsNeeded, playerEmpire.Credits.CurrentValue);

            var confirmResult = MessageDialog.Show(
                ResourceManager.GetString("CONFIRM_RUSH_BUILDING_HEADER"),
                confirmationMessage,
                MessageDialogButtons.YesNo);

            if (confirmResult != MessageDialogResult.Yes)
                return;

            _logger.Log(string.Format("Rushing production for Colony: {0}", colony.Name), Category.Debug, Priority.None);

            project.IsRushed = true;
            PlayerOrderService.Instance.AddOrder(new RushProductionOrder(project.ProductionCenter));
        }

        #endregion
    }
}