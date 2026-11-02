using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using Rect = System.Windows.Rect;
using NStCut.Core.Constants;
using NStCut.Core.Geometry;
using NStCut.Core.Models;

namespace NStCut.App.Services;

public static class SheetThumbnailService
{
    // Рендер через DrawingVisual/DrawingContext вместо Canvas + отдельных Rectangle-элементов
    // (UIElement с полноценным Measure/Arrange layout pass на каждую деталь) — то же изображение
    // получается без затрат на построение и раскладку визуального дерева WPF-контролов
    // (docs/Audit_Performance.md, issue P7).
    public static byte[] Render(SheetInstance sheet, double scale = 2.0, bool showPartNumbers = false)
    {
        var width = sheet.Material.Length * scale;
        var height = sheet.Material.Width * scale;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(null, new Pen(Brushes.Black, 2), new Rect(0, 0, width, height));

            var partPen = new Pen(Brushes.Blue, 1);
            var partNumbers = showPartNumbers ? SheetPartNumbering.Build(sheet) : null;
            foreach (var placed in sheet.Parts)
            {
                var left = placed.Bounds.Left * scale;
                var top = (sheet.Material.Width - placed.Bounds.Top) * scale;
                var rect = new Rect(left, top, placed.Bounds.Width * scale, placed.Bounds.Height * scale);
                dc.DrawRectangle(Brushes.LightBlue, partPen, rect);
                if (partNumbers != null && partNumbers.TryGetValue(placed.Source, out var number))
                {
                    var fontSize = Math.Max(14, Math.Min(rect.Width, rect.Height) * 0.4);
                    var label = new FormattedText(number.ToString(CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                        new Typeface("Arial"), fontSize, Brushes.Black, 1);
                    dc.DrawText(label, new System.Windows.Point(
                        rect.Left + (rect.Width - label.Width) / 2,
                        rect.Top + (rect.Height - label.Height) / 2));
                }
            }

            var localization = LocalizationService.Instance;
            var direction = !sheet.Material.HasGrain ? localization.NoGrain :
                sheet.Material.GrainDirection == GrainDirection.Horizontal ? localization.GrainHorizontal : localization.GrainVertical;
            dc.DrawText(new FormattedText(direction, CultureInfo.CurrentUICulture, System.Windows.FlowDirection.LeftToRight,
                new Typeface("Arial"), 12, Brushes.Black, 1), new System.Windows.Point(8, 8));
            if (sheet.Material.HasGrain)
            {
                var center = new System.Windows.Point(width / 2, height / 2);
                var half = Math.Min(
                    sheet.Material.GrainDirection == GrainDirection.Horizontal ? width / 4 : height / 4,
                    ExportPresentationConstants.MaxArrowHalfLength * scale);
                var start = sheet.Material.GrainDirection == GrainDirection.Horizontal
                    ? new System.Windows.Point(center.X - half, center.Y)
                    : new System.Windows.Point(center.X, center.Y + half);
                var end = sheet.Material.GrainDirection == GrainDirection.Horizontal
                    ? new System.Windows.Point(center.X + half, center.Y)
                    : new System.Windows.Point(center.X, center.Y - half);

                var arrowPen = new Pen(Brushes.Black, ExportPresentationConstants.GrainArrowLineThickness);
                dc.DrawLine(arrowPen, start, end);

                var headLength = ExportPresentationConstants.ArrowHeadLength * scale;
                var (head1, head2) = ArrowGeometryHelper.GetArrowHead(
                    new NStCut.Core.Geometry.Point(start.X, start.Y),
                    new NStCut.Core.Geometry.Point(end.X, end.Y),
                    headLength,
                    ExportPresentationConstants.ArrowHeadAngle);
                dc.DrawLine(arrowPen, end, new System.Windows.Point(head1.X, head1.Y));
                dc.DrawLine(arrowPen, end, new System.Windows.Point(head2.X, head2.Y));
            }
        }

        var render = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(render));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
