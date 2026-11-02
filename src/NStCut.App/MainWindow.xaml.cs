using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NStCut.App.Models;
using NStCut.App.ViewModels;
using NStCut.App.Views;
using NStCut.Core.Models;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using Button = System.Windows.Controls.Button;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;

namespace NStCut.App;

public partial class MainWindow : Window
{
    // Клик с интерактивного контрола ячейки (кнопка/ComboBox/чекбокс/текстовое поле) не должен
    // делать строку кандидатом на перетаскивание.
    private readonly DataGridRowDragHelper<PartRow> _dragHelper =
        new(source => source is ButtonBase or ComboBox or CheckBox or TextBox);

    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;
    }

    // Сопоставление стабильного ключа (для хранения в AppSettings.PartsGridColumnWidths) с
    // конкретной колонкой PartsGrid. Ключи — это x:Name колонок из MainWindow.xaml; заголовки
    // не подходят на роль ключа, так как локализованы и меняются при переключении языка.
    private (string Key, DataGridColumn Column)[] PartsGridColumnKeys => new (string, DataGridColumn)[]
    {
        ("ColSeq", ColSeq),
        ("ColEnabled", ColEnabled),
        ("ColNumber", ColNumber),
        ("ColTitle", ColTitle),
        ("ColQty", ColQty),
        ("ColDxf", ColDxf),
        ("ColDimensions", ColDimensions),
        ("ColMaterial", ColMaterial),
        ("ColGrain", ColGrain),
        ("ColRotate", ColRotate),
        ("ColStatus", ColStatus)
    };

    /// <summary>Восстанавливает ширины столбцов PartsGrid, сохранённые в прошлом запуске.</summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        foreach (var (key, column) in PartsGridColumnKeys)
        {
            if (vm.PartsGridColumnWidths.TryGetValue(key, out var width) && width > 0)
                column.Width = new DataGridLength(width);
        }
    }

    /// <summary>Сохраняет текущие ширины столбцов PartsGrid и остальные настройки — вызывается при закрытии главного окна.</summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            foreach (var (key, column) in PartsGridColumnKeys)
                vm.PartsGridColumnWidths[key] = column.ActualWidth;

            vm.SaveAppSettings();
        }
    }

    private void PartRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row)
            _dragHelper.OnPreviewMouseLeftButtonDown(row, e);
    }

    /// <summary>Начинает Drag&amp;Drop строки в другой раздел, если мышь ушла за порог перетаскивания.</summary>
    private void PartRow_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is DependencyObject source)
            _dragHelper.OnMouseMove(source, e);
    }

    private void PartRow_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(PartRow)))
        {
            if (sender is not DataGridRow targetRow || targetRow.Item is not PartRow targetPart)
                return;

            var dragged = (PartRow)e.Data.GetData(typeof(PartRow))!;
            if (ReferenceEquals(dragged, targetPart))
                return;

            if (DataContext is MainViewModel vm)
                vm.MoveRowToMaterial(dragged, targetPart.MaterialId);
            e.Handled = true;
            return;
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        var dxfFiles = DroppedDxfMapper.FilterDxfFiles(files);
        if (dxfFiles.Count != 1)
            return;

        if (sender is not DataGridRow dropTargetRow || dropTargetRow.Item is not PartRow dropTargetPart || DataContext is not MainViewModel dropVm)
            return;

        dropVm.AssignDroppedDxfToRow(dropTargetPart, dxfFiles[0]);
    }

    private void PartsGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || DataContext is not MainViewModel vm)
            return;

        vm.AddDroppedDxfFiles(files);
        e.Handled = true;
    }

    private void PartsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || !(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            return;
        if (sender is not DataGrid grid || grid.CurrentCell.Column != ColDxf)
            return;
        if (grid.CurrentItem is not PartRow row || DataContext is not MainViewModel vm)
            return;
        if (!vm.CopyDxfPathCommand.CanExecute(row))
            return;

        vm.CopyDxfPathCommand.Execute(row);
        e.Handled = true;
    }

    /// <summary>Открывает выпадающее меню "Імпорт Excel" у сплит-кнопки "Вставити ▾" (docs/UX_Audit.md, разметка v3).</summary>
    private void OpenImportMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }
}
