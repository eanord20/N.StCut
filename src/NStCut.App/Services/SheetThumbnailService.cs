using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Size = System.Windows.Size;
using NStCut.Core.Models;

namespace NStCut.App.Services;

public static class SheetThumbnailService
{
    public static byte[] Render(SheetInstance sheet, double scale = 2.0)
    {
        var width = sheet.Material.Length * scale;
        var height = sheet.Material.Width * scale;

        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.White
        };

        var border = new System.Windows.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Stroke = Brushes.Black,
            StrokeThickness = 2
        };
        canvas.Children.Add(border);

        foreach (var placed in sheet.Parts)
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = placed.Bounds.Width * scale,
                Height = placed.Bounds.Height * scale,
                Stroke = Brushes.Blue,
                StrokeThickness = 1,
                Fill = Brushes.LightBlue
            };
            Canvas.SetLeft(rect, placed.Bounds.Left * scale);
            Canvas.SetTop(rect, (sheet.Material.Width - placed.Bounds.Top) * scale);
            canvas.Children.Add(rect);
        }

        canvas.Measure(new Size(width, height));
        canvas.Arrange(new Rect(0, 0, width, height));

        var render = new RenderTargetBitmap(
            (int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        render.Render(canvas);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(render));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
