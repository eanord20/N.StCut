using System.Collections.ObjectModel;
using System.Windows;
using NStCut.App.ViewModels;

namespace NStCut.App.Views;

public partial class CandidateSelectWindow : Window
{
    public CandidateSelectWindow(IReadOnlyList<string> candidates)
    {
        Candidates = new ObservableCollection<string>(candidates);
    }

    public ObservableCollection<string> Candidates { get; }
    public string? SelectedPath { get; set; }
}
