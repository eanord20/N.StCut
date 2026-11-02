using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NStCut.App.Models;
using NStCut.App.ViewModels;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

namespace NStCut.App.Views;

public partial class MaterialsWindow : Window
{
    public MaterialsWindow()
    {
        InitializeComponent();
        Loaded += MaterialsWindow_Loaded;
        Closing += MaterialsWindow_Closing;
    }

    private MaterialsViewModel Vm => (MaterialsViewModel)DataContext;

    // Ключи — x:Name колонок LibraryGrid (заголовки локализованы, как ключи не годятся).
    private (string Key, DataGridColumn Column)[] ColumnKeys => new (string, DataGridColumn)[]
    {
        ("ColName", ColName),
        ("ColLength", ColLength),
        ("ColWidth", ColWidth),
        ("ColThickness", ColThickness),
        ("ColMaterialMargin", ColMaterialMargin),
        ("ColMaterialGap", ColMaterialGap),
        ("ColHasGrain", ColHasGrain),
        ("ColGrain", ColGrain),
        ("ColMarginRelax", ColMarginRelax)
    };

    private void MaterialsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (var (key, column) in ColumnKeys)
        {
            if (Vm.ColumnWidths.TryGetValue(key, out var width) && width > 0)
                column.Width = new DataGridLength(width);
        }
    }

    private void MaterialsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        foreach (var (key, column) in ColumnKeys)
            Vm.ColumnWidths[key] = column.ActualWidth;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var localization = NStCut.App.Services.LocalizationService.Instance;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = localization.MaterialLibraryFilter };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            var counts = Vm.ImportMaterials(NStCut.App.Services.MaterialsLibraryService.LoadForImport(dialog.FileName));
            TransferStatus.Text = string.Format(localization.StatusMaterialsImportedFormat, counts.Added, counts.Skipped);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "N.StCut", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var localization = NStCut.App.Services.LocalizationService.Instance;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = localization.MaterialLibraryFilter,
            FileName = "materials-library.json",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            var materials = Vm.LibraryMaterials.Select(m => m.ToMaterial()).ToList();
            NStCut.App.Services.MaterialsLibraryService.Validate(materials);
            NStCut.App.Services.MaterialsLibraryService.SaveAll(dialog.FileName, materials);
            TransferStatus.Text = string.Format(localization.StatusMaterialsExportedFormat, dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "N.StCut", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

internal static class VisualTreeExtensions
{
    public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
            parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }
}

