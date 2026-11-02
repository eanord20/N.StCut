using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using NStCut.App.Commands;
using NStCut.App.Models;
using NStCut.App.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace NStCut.App.ViewModels;

/// <summary>
/// Двухпанельный редактор материалов: слева — общая библиотека (CRUD), справа — материалы,
/// выбранные для текущего проекта (только перенос и порядок, порядок определяет разделы в главной таблице).
/// </summary>
public sealed class MaterialsViewModel : INotifyPropertyChanged
{
    private readonly IMaterialsLibraryService _libraryService;

    public ObservableCollection<MaterialRow> LibraryMaterials { get; } = new();
    public ObservableCollection<MaterialRow> ProjectMaterials { get; } = new();

    public MaterialRow? SelectedLibraryMaterial { get; set; }
    public MaterialRow? SelectedProjectMaterial { get; set; }

    public ICommand AddToLibraryCommand { get; }
    public ICommand DeleteFromLibraryCommand { get; }
    public ICommand MoveToProjectCommand { get; }
    public ICommand MoveToLibraryCommand { get; }
    public ICommand MoveProjectUpCommand { get; }
    public ICommand MoveProjectDownCommand { get; }
    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CopyCommand { get; }

    public bool? DialogResult { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Ширины столбцов таблицы библиотеки (передаётся из MainViewModel, хранится в AppSettings).</summary>
    public Dictionary<string, double> ColumnWidths { get; } = new();

    public MaterialsViewModel(IEnumerable<MaterialRow> projectMaterials, IMaterialsLibraryService? libraryService = null,
        Dictionary<string, double>? columnWidths = null)
    {
        _libraryService = libraryService ?? new MaterialsLibraryService();
        if (columnWidths != null)
            ColumnWidths = columnWidths;

        foreach (var m in _libraryService.Load())
            LibraryMaterials.Add(MaterialRow.FromMaterial(m));

        foreach (var pm in projectMaterials)
        {
            var libraryMatch = LibraryMaterials.FirstOrDefault(l => l.Id == pm.Id);
            if (libraryMatch != null)
            {
                ProjectMaterials.Add(libraryMatch);
            }
            else
            {
                // Материал был в проекте, но отсутствует в библиотеке (например, старый проект) — добавляем и туда.
                LibraryMaterials.Add(pm);
                ProjectMaterials.Add(pm);
            }
        }

        AddToLibraryCommand = new RelayCommand(_ => AddToLibrary());
        DeleteFromLibraryCommand = new RelayCommand(_ => DeleteFromLibrary(), _ => SelectedLibraryMaterial != null);
        MoveToProjectCommand = new RelayCommand(_ => MoveSelectedToProject(), _ => SelectedLibraryMaterial != null && !ProjectMaterials.Contains(SelectedLibraryMaterial));
        MoveToLibraryCommand = new RelayCommand(_ => RemoveSelectedFromProject(), _ => SelectedProjectMaterial != null);
        MoveProjectUpCommand = new RelayCommand(_ => MoveSelectedProject(-1), _ => CanMoveSelectedProject(-1));
        MoveProjectDownCommand = new RelayCommand(_ => MoveSelectedProject(1), _ => CanMoveSelectedProject(1));
        OkCommand = new RelayCommand(_ => Ok());
        CancelCommand = new RelayCommand(_ => { DialogResult = false; OnPropertyChanged(nameof(DialogResult)); });
        CopyCommand = new RelayCommand(_ => CopySelected(), _ => SelectedLibraryMaterial != null);
    }

    public void MoveToProject(MaterialRow material)
    {
        if (!ProjectMaterials.Contains(material))
            ProjectMaterials.Add(material);
    }

    public void RemoveFromProject(MaterialRow material)
    {
        ProjectMaterials.Remove(material);
    }

    /// <summary>Импортирует материалы в библиотеку, пропуская дубликаты по Id и имени.</summary>
    public (int Added, int Skipped) ImportMaterials(IEnumerable<NStCut.Core.Models.SheetMaterial> materials)
    {
        var added = 0;
        var skipped = 0;
        foreach (var m in materials)
        {
            if (LibraryMaterials.Any(l => l.Id == m.Id ||
                string.Equals(l.Name, m.Name, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            LibraryMaterials.Add(MaterialRow.FromMaterial(m));
            added++;
        }
        return (added, skipped);
    }

    public void ReorderProject(MaterialRow moved, MaterialRow target)
    {
        if (ReferenceEquals(moved, target)) return;
        var oldIndex = ProjectMaterials.IndexOf(moved);
        var newIndex = ProjectMaterials.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0) return;
        ProjectMaterials.Move(oldIndex, newIndex);
    }

    private void AddToLibrary()
    {
        var material = new MaterialRow { Name = "New material", Length = 1000, Width = 1000 };
        LibraryMaterials.Add(material);
        SelectedLibraryMaterial = material;
        OnPropertyChanged(nameof(SelectedLibraryMaterial));
    }

    private void CopySelected()
    {
        if (SelectedLibraryMaterial == null) return;

        var original = SelectedLibraryMaterial;
        var copy = new MaterialRow
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = FindFreeCopyName(original.Name),
            Length = original.Length,
            Width = original.Width,
            Thickness = original.Thickness,
            HasGrain = original.HasGrain,
            GrainDirection = original.GrainDirection
        };

        var index = LibraryMaterials.IndexOf(original);
        if (index >= 0)
            LibraryMaterials.Insert(index + 1, copy);
        else
            LibraryMaterials.Add(copy);

        SelectedLibraryMaterial = copy;
        OnPropertyChanged(nameof(SelectedLibraryMaterial));
    }

    private string FindFreeCopyName(string baseName)
    {
        var names = new HashSet<string>(LibraryMaterials.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
        for (var i = 1; ; i++)
        {
            var candidate = $"{baseName}_{i}";
            if (!names.Contains(candidate))
                return candidate;
        }
    }

    private void DeleteFromLibrary()
    {
        if (SelectedLibraryMaterial == null) return;
        if (ProjectMaterials.Contains(SelectedLibraryMaterial))
        {
            MessageBox.Show(
                "Материал используется в текущем проекте — сначала убери его из проектного списка (кнопка '<').",
                "N.StCut", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        LibraryMaterials.Remove(SelectedLibraryMaterial);
    }

    private void MoveSelectedToProject()
    {
        if (SelectedLibraryMaterial != null)
            MoveToProject(SelectedLibraryMaterial);
    }

    private void RemoveSelectedFromProject()
    {
        if (SelectedProjectMaterial != null)
            RemoveFromProject(SelectedProjectMaterial);
    }

    private bool CanMoveSelectedProject(int direction)
    {
        if (SelectedProjectMaterial == null) return false;
        var index = ProjectMaterials.IndexOf(SelectedProjectMaterial);
        var newIndex = index + direction;
        return index >= 0 && newIndex >= 0 && newIndex < ProjectMaterials.Count;
    }

    private void MoveSelectedProject(int direction)
    {
        if (SelectedProjectMaterial == null) return;
        var index = ProjectMaterials.IndexOf(SelectedProjectMaterial);
        var newIndex = index + direction;
        if (index < 0 || newIndex < 0 || newIndex >= ProjectMaterials.Count) return;
        ProjectMaterials.Move(index, newIndex);
    }

    private void Ok()
    {
        _libraryService.Save(LibraryMaterials.Select(m => m.ToMaterial()).ToList());
        DialogResult = true;
        OnPropertyChanged(nameof(DialogResult));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
