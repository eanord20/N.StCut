using System.Windows;

namespace NStCut.App.Views;

public partial class CandidateSelectWindow : Window
{
    public CandidateSelectWindow(IReadOnlyList<string> candidates)
    {
        InitializeComponent();
        CandidatesListBox.ItemsSource = candidates;
    }

    public string? SelectedPath => CandidatesListBox.SelectedItem as string;

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
