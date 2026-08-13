using System.Windows;

namespace NStCut.App.Views;

public partial class MaterialsWindow : Window
{
    public MaterialsWindow()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
