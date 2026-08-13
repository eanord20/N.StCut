using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NStCut.App.Commands;
using NStCut.App.Models;

namespace NStCut.App.ViewModels;

public sealed class MaterialsViewModel : INotifyPropertyChanged
{
    public ObservableCollection<MaterialRow> Materials { get; } = new();
    public MaterialRow? SelectedMaterial { get; set; }

    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    public bool? DialogResult { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MaterialsViewModel(IEnumerable<MaterialRow> materials)
    {
        foreach (var m in materials)
            Materials.Add(m);

        AddCommand = new RelayCommand(_ => Add());
        DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedMaterial != null);
        OkCommand = new RelayCommand(_ => { DialogResult = true; OnPropertyChanged(nameof(DialogResult)); });
        CancelCommand = new RelayCommand(_ => { DialogResult = false; OnPropertyChanged(nameof(DialogResult)); });
    }

    private void Add()
    {
        Materials.Add(new MaterialRow { Name = "New material", Length = 1000, Width = 1000 });
    }

    private void Delete()
    {
        if (SelectedMaterial != null)
            Materials.Remove(SelectedMaterial);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
