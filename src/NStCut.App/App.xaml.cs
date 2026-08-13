using System.Windows;
using NStCut.App.ViewModels;
using NStCut.Dxf.Services;
using NStCut.Nesting.Services;
using NStCut.Reports.Services;
using Application = System.Windows.Application;

namespace NStCut.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var vm = new MainViewModel(
            new BoundingBoxShelfEngine(),
            new DxfGeometryService(),
            new DxfExportService(),
            new ReportService());

        var mainWindow = new MainWindow { DataContext = vm };
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
