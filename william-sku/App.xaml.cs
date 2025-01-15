using MahApps.Metro.Controls.Dialogs;
using OfficeOpenXml;
using System.Configuration;
using System.Data;
using System.Windows;
using william_sku.Views;

namespace william_sku
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<Main>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var regionManager = Container.Resolve<IRegionManager>();
            regionManager.RegisterViewWithRegion("MainRegion", typeof(Views.Data));
            regionManager.RegisterViewWithRegion("MainRegion", typeof(Views.Settings));

            containerRegistry.RegisterSingleton<Data.Database>();
            containerRegistry.RegisterDialogWindow<MetroDialog>();
            containerRegistry.RegisterDialog<Views.Settings>();
            containerRegistry.RegisterDialog<Views.Search>();
            containerRegistry.RegisterInstance(DialogCoordinator.Instance);
        }
    }

}
