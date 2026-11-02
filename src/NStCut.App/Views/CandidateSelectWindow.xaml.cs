using System.Windows;
using NStCut.App.Services;

namespace NStCut.App.Views;

public partial class CandidateSelectWindow : Window
{
    public CandidateSelectWindow(IReadOnlyList<string> candidates)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowPlacementHelper.ApplyToWindow(this, 0.8, 0.7, 600, 300);
        CandidatesListBox.ItemsSource = candidates;
    }

    public string? SelectedPath => CandidatesListBox.SelectedItem as string;

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
