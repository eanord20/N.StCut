using System.Windows;
using System.Windows.Threading;
using NStCut.App.Services;
using NStCut.App.ViewModels;
using NStCut.Dxf.Services;
using NStCut.Nesting.Services;
using NStCut.Reports.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace NStCut.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;

        var vm = new MainViewModel(
            new MultiPassNestingEngine(new BoundingBoxShelfEngine()),
            new DxfGeometryService(),
            new DxfExportService(),
            new ReportService());

        var mainWindow = new MainWindow { DataContext = vm };
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>
    /// Последний рубеж защиты от падения процесса: необработанное исключение из любого места
    /// приложения показывается пользователю вместо аварийного завершения (см. docs/Audit_Reliability.md, R1).
    /// </summary>
    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, LocalizationService.Instance.ErrorTitleUnexpected, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
