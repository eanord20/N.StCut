using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Rectangle = System.Windows.Shapes.Rectangle;
using NStCut.App.Services;
using NStCut.App.ViewModels;

namespace NStCut.App.Views;

public partial class SheetPreviewWindow : Window
{
    private PreviewPartViewModel? _draggedPart;
    private SheetPreviewItemViewModel? _draggedSheet;
    private double _originalLeft;
    private double _originalTop;
    private Point _startMouse;
    private Canvas? _dragCanvas;

    public SheetPreviewWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            if (Owner == null && System.Windows.Application.Current?.MainWindow != null)
                Owner = System.Windows.Application.Current.MainWindow;

            WindowPlacementHelper.ApplyToWindow(this, 0.9, 0.9, 800, 600);
        };
    }

    private void OnPartMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle rectangle)
            return;

        if (rectangle.DataContext is not PreviewPartViewModel part)
            return;

        var contentPresenter = rectangle.FindParent<ContentPresenter>();
        if (contentPresenter is null)
            return;

        if (ItemsControl.ItemsControlFromItemContainer(contentPresenter) is not ItemsControl itemsControl)
            return;

        if (itemsControl.DataContext is not SheetPreviewItemViewModel sheet)
            return;

        var canvas = contentPresenter.FindParent<Canvas>();
        if (canvas is null)
            return;

        _draggedPart = part;
        _draggedSheet = sheet;
        _originalLeft = part.Left;
        _originalTop = part.Top;
        _dragCanvas = canvas;
        _startMouse = Mouse.GetPosition(canvas);

        Mouse.Capture(rectangle);
        e.Handled = true;
    }

    private void OnPartMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedPart is null || _dragCanvas is null)
            return;

        var current = Mouse.GetPosition(_dragCanvas);
        _draggedPart.Left = _originalLeft + (current.X - _startMouse.X);
        _draggedPart.Top = _originalTop + (current.Y - _startMouse.Y);
        e.Handled = true;
    }

    private void OnPartMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedPart is null || _draggedSheet is null)
            return;

        var newLeft = _draggedPart.Left;
        var newTop = _draggedPart.Top;

        var (snappedLeft, snappedTop) = _draggedSheet.SnapToNearest(_draggedPart, newLeft, newTop);

        if (_draggedSheet.CanPlace(_draggedPart, snappedLeft, snappedTop))
        {
            _draggedPart.Left = snappedLeft;
            _draggedPart.Top = snappedTop;
            _draggedSheet.ApplyPartPosition(_draggedPart, snappedLeft, snappedTop);
        }
        else
        {
            _draggedPart.Left = _originalLeft;
            _draggedPart.Top = _originalTop;
        }

        _draggedPart = null;
        _draggedSheet = null;
        _dragCanvas = null;

        Mouse.Capture(null);
        e.Handled = true;
    }

    private void OnPartMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle rectangle)
            return;

        if (rectangle.DataContext is not PreviewPartViewModel part)
            return;

        var contentPresenter = rectangle.FindParent<ContentPresenter>();
        if (contentPresenter is null)
            return;

        if (ItemsControl.ItemsControlFromItemContainer(contentPresenter) is not ItemsControl itemsControl)
            return;

        if (itemsControl.DataContext is not SheetPreviewItemViewModel sheet)
            return;

        sheet.TryRotate(part);
        e.Handled = true;
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);

        if (DataContext is not SheetPreviewViewModel vm)
            return;

        if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
            return;

        const double step = 0.1;
        var zoomDelta = e.Delta / 120.0 * step;
        vm.Zoom = Math.Max(0.1, Math.Min(2.0, vm.Zoom + zoomDelta));
        e.Handled = true;
    }
}
