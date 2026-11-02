using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace NStCut.App.Views;

/// <summary>
/// Общая логика начала Drag&amp;Drop одной строки DataGrid: запоминает кандидата на
/// PreviewMouseLeftButtonDown, инициирует DoDragDrop при выходе за системный порог перемещения мыши
/// в MouseMove. Устраняет дублирование между MainWindow и MaterialsWindow
/// (см. docs/Audit_Architecture.md, issue D1 / docs/Code_Audit_UI.md, issue C4).
/// </summary>
internal sealed class DataGridRowDragHelper<T> where T : class
{
    private readonly Func<object, bool>? _isInteractiveSource;
    private T? _dragCandidate;
    private Point _dragStartPoint;

    /// <param name="isInteractiveSource">
    /// Необязательная проверка: если клик пришёл с интерактивного контрола ячейки (кнопка/ComboBox/...),
    /// строка не должна становиться кандидатом на перетаскивание.
    /// </param>
    public DataGridRowDragHelper(Func<object, bool>? isInteractiveSource = null)
    {
        _isInteractiveSource = isInteractiveSource;
    }

    public void OnPreviewMouseLeftButtonDown(DataGridRow row, MouseButtonEventArgs e)
    {
        if (row.Item is not T item || (_isInteractiveSource != null && _isInteractiveSource(e.OriginalSource)))
        {
            _dragCandidate = null;
            return;
        }

        _dragStartPoint = e.GetPosition(null);
        _dragCandidate = item;
    }

    public void OnMouseMove(DependencyObject source, MouseEventArgs e)
    {
        if (_dragCandidate == null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var candidate = _dragCandidate;
        _dragCandidate = null;
        DragDrop.DoDragDrop(source, new DataObject(typeof(T), candidate), DragDropEffects.Move);
    }
}
