using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using NStCut.App.Services;

namespace NStCut.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational?.Split('+')[0]
                      ?? assembly.GetName().Version?.ToString() ?? "?";
        var localization = LocalizationService.Instance;
        VersionText.Text = string.Format(localization.AboutVersionFormat, version);
        CopyrightText.Text = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright
                             ?? string.Empty;
    }

    private void License_Click(object sender, RoutedEventArgs e)
    {
        var path = FindLicenseFile();
        if (path is not null)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return;
        }

        MessageBox.Show(this,
            "GNU General Public License v3 or later\nhttps://www.gnu.org/licenses/gpl-3.0.html",
            "N.StCut", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string? FindLicenseFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "LICENSE");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
