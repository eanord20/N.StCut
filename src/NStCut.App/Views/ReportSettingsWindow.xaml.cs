using System.Windows;

namespace NStCut.App.Views;

public partial class ReportSettingsWindow : Window
{
    public ReportSettingsWindow()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
