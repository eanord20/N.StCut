using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace NStCut.App.Services;

public static class WindowPlacementHelper
{
    public static System.Windows.Size ComputeWindowSize(Rect workArea, double widthFraction, double heightFraction, double minWidth = 0, double minHeight = 0)
    {
        var width = Math.Max(minWidth, workArea.Width * widthFraction);
        var height = Math.Max(minHeight, workArea.Height * heightFraction);

        return new System.Windows.Size(Math.Min(width, workArea.Width), Math.Min(height, workArea.Height));
    }

    public static Rect CenterInWorkArea(Rect workArea, System.Windows.Size size)
    {
        var left = workArea.Left + (workArea.Width - size.Width) / 2.0;
        var top = workArea.Top + (workArea.Height - size.Height) / 2.0;

        return new Rect(left, top, size.Width, size.Height);
    }

    public static Rect ConvertPixelsToDips(Rect pixels, double scaleX, double scaleY)
    {
        return new Rect(
            pixels.X / scaleX,
            pixels.Y / scaleY,
            pixels.Width / scaleX,
            pixels.Height / scaleY);
    }

    public static void ApplyToWindow(Window window, double widthFraction, double heightFraction, double minWidth = 0, double minHeight = 0)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var screen = Screen.FromHandle(handle);
        var workingAreaPixels = screen.WorkingArea;

        var scaleX = 1.0;
        var scaleY = 1.0;
        var source = HwndSource.FromHwnd(handle);
        if (source?.CompositionTarget != null)
        {
            scaleX = source.CompositionTarget.TransformFromDevice.M11;
            scaleY = source.CompositionTarget.TransformFromDevice.M22;
        }

        var workArea = ConvertPixelsToDips(
            new Rect(workingAreaPixels.X, workingAreaPixels.Y, workingAreaPixels.Width, workingAreaPixels.Height),
            scaleX,
            scaleY);

        var size = ComputeWindowSize(workArea, widthFraction, heightFraction, minWidth, minHeight);
        var rect = CenterInWorkArea(workArea, size);

        window.Width = rect.Width;
        window.Height = rect.Height;
        window.Left = rect.Left;
        window.Top = rect.Top;
    }
}
