using System.Threading;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using NStCut.Core.Constants;
using NStCut.Core.Contracts;
using NStCut.Core.Geometry;
using NStCut.Core.Models;

namespace NStCut.Dxf.Services;

public sealed class DxfExportService : IDxfExportService
{
    public void WriteSheet(string outputPath, SheetInstance sheet, double partGapMm, double sheetMarginMm, DxfExportLabels? labels = null, CancellationToken cancellationToken = default)
        => WriteDocument(outputPath, new[] { (sheet, new Vector2(0, 0)) }, labels, cancellationToken);

    public void WriteSheets(IReadOnlyList<(string OutputPath, SheetInstance Sheet)> sheets, double partGapMm, double sheetMarginMm, DxfExportLabels? labels = null, CancellationToken cancellationToken = default)
    {
        var sourceCache = new Dictionary<string, DxfDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in sheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteDocument(item.OutputPath, new[] { (item.Sheet, new Vector2(0, 0)) }, labels, cancellationToken, sourceCache);
        }
    }

    public void WriteCombined(string outputPath, IReadOnlyList<SheetInstance> sheets, double partGapMm, double sheetMarginMm, DxfExportLabels labels, CancellationToken cancellationToken = default)
        => WriteDocument(outputPath, CreateLayout(sheets), labels, cancellationToken);

    private static IReadOnlyList<(SheetInstance Sheet, Vector2 Origin)> CreateLayout(IReadOnlyList<SheetInstance> sheets)
    {
        var result = new List<(SheetInstance, Vector2)>();
        var top = 0d;
        foreach (var group in sheets
                     .GroupBy(s => (s.Material.Length, s.Material.Width))
                     .OrderBy(g => g.Key.Length * g.Key.Width)
                     .ThenBy(g => g.Key.Length)
                     .ThenBy(g => g.Key.Width))
        {
            var ordered = group.OrderBy(s => s.Material.Name, StringComparer.Ordinal)
                .ThenBy(s => s.Material.Id, StringComparer.Ordinal).ThenBy(s => s.Index).ToList();
            var bottom = top - group.Key.Width;
            var x = 0d;
            foreach (var sheet in ordered)
            {
                result.Add((sheet, new Vector2(x, bottom)));
                x += sheet.Material.Length + ExportPresentationConstants.SheetSpacing;
            }
            top = bottom - ExportPresentationConstants.SheetSpacing - ExportPresentationConstants.AnnotationBandHeight;
        }
        return result;
    }

    private static void WriteDocument(string outputPath, IReadOnlyList<(SheetInstance Sheet, Vector2 Origin)> layout,
        DxfExportLabels? labels, CancellationToken cancellationToken, Dictionary<string, DxfDocument>? sourceCache = null)
    {
        var doc = new DxfDocument();
        sourceCache ??= new Dictionary<string, DxfDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sheet, origin) in layout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var boundary = new Polyline2D(new[]
            {
                origin, new Vector2(origin.X + sheet.Material.Length, origin.Y),
                new Vector2(origin.X + sheet.Material.Length, origin.Y + sheet.Material.Width),
                new Vector2(origin.X, origin.Y + sheet.Material.Width)
            }, true) { Layer = new Layer("SheetBoundary") { Color = AciColor.Yellow } };
            doc.Entities.Add(boundary);
            foreach (var placed in sheet.Parts)
                WritePlacedPart(doc, placed, origin, sourceCache);
            if (labels is not null)
                AddPresentation(doc, sheet, origin, labels);
        }
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        doc.Save(outputPath);
    }

    private static void AddPresentation(DxfDocument doc, SheetInstance sheet, Vector2 origin, DxfExportLabels labels)
    {
        var direction = !sheet.Material.HasGrain ? labels.NoGrain :
            sheet.Material.GrainDirection == GrainDirection.Horizontal ? labels.Horizontal : labels.Vertical;
        var materialName = string.IsNullOrWhiteSpace(sheet.Material.Name) ? sheet.Material.Id : sheet.Material.Name;
        if (string.IsNullOrWhiteSpace(materialName)) materialName = labels.Material;
        var boundaryTop = origin.Y + sheet.Material.Width;
        var labelPosition = new Vector2(origin.X, boundaryTop + ExportPresentationConstants.LabelOffset);
        doc.Entities.Add(new Text($"{labels.Material}: {materialName} | {direction} | {labels.Sheet} {sheet.Index}",
            labelPosition, ExportPresentationConstants.LabelFontHeight)
            { Layer = new Layer("SheetLabels") });
        if (!sheet.Material.HasGrain) return;

        var arrowBaseline = boundaryTop + ExportPresentationConstants.ArrowBaselineOffset;
        var half = Math.Min(
            sheet.Material.GrainDirection == GrainDirection.Horizontal ? sheet.Material.Length / 4 : sheet.Material.Width / 4,
            ExportPresentationConstants.MaxArrowHalfLength);

        var layer = new Layer("GrainArrow");

        if (sheet.Material.GrainDirection == GrainDirection.Horizontal)
        {
            var centerX = origin.X + sheet.Material.Length / 2;
            var start = new Vector2(centerX - half, arrowBaseline);
            var end = new Vector2(centerX + half, arrowBaseline);

            doc.Entities.Add(new Line(start, end) { Layer = layer });

            var (head1, head2) = ArrowGeometryHelper.GetArrowHead(
                new NStCut.Core.Geometry.Point(start.X, start.Y),
                new NStCut.Core.Geometry.Point(end.X, end.Y),
                ExportPresentationConstants.ArrowHeadLength,
                ExportPresentationConstants.ArrowHeadAngle);
            doc.Entities.Add(new Line(new Vector2(end.X, end.Y), new Vector2(head1.X, head1.Y)) { Layer = layer });
            doc.Entities.Add(new Line(new Vector2(end.X, end.Y), new Vector2(head2.X, head2.Y)) { Layer = layer });
        }
        else
        {
            var centerX = origin.X + sheet.Material.Length / 2;
            var start = new Vector2(centerX, arrowBaseline);
            var end = new Vector2(centerX, arrowBaseline + 2 * half);

            doc.Entities.Add(new Line(start, end) { Layer = layer });

            var (head1, head2) = ArrowGeometryHelper.GetArrowHead(
                new NStCut.Core.Geometry.Point(start.X, start.Y),
                new NStCut.Core.Geometry.Point(end.X, end.Y),
                ExportPresentationConstants.ArrowHeadLength,
                ExportPresentationConstants.ArrowHeadAngle);
            doc.Entities.Add(new Line(new Vector2(end.X, end.Y), new Vector2(head1.X, head1.Y)) { Layer = layer });
            doc.Entities.Add(new Line(new Vector2(end.X, end.Y), new Vector2(head2.X, head2.Y)) { Layer = layer });
        }
    }

    private static void WritePlacedPart(DxfDocument targetDoc, PlacedPart placed, Vector2 origin, Dictionary<string, DxfDocument> sourceDocCache)
    {
        if (string.IsNullOrWhiteSpace(placed.Source.DxfPath))
            throw new ArgumentException($"Placed part '{placed.Source.DisplayName}' has no DXF path.", nameof(placed));
        if (!File.Exists(placed.Source.DxfPath))
            throw new FileNotFoundException($"DXF file not found for placed part '{placed.Source.DisplayName}': {placed.Source.DxfPath}", placed.Source.DxfPath);

        if (!sourceDocCache.TryGetValue(placed.Source.DxfPath, out var sourceDoc))
        {
            sourceDoc = DxfDocument.Load(placed.Source.DxfPath);
            sourceDocCache[placed.Source.DxfPath] = sourceDoc;
        }
        var sourceBounds = placed.Source.Bounds;
        var toOrigin = Matrix4.Translation(new Vector3(-sourceBounds.Left, -sourceBounds.Bottom, 0));
        var rotation = Matrix4.RotationZ(placed.Rotation * Math.PI / 180.0);
        var rotatedMin = GetRotatedBoundsMin(sourceBounds, placed.Rotation);
        var compensate = Matrix4.Translation(new Vector3(-rotatedMin.X, -rotatedMin.Y, 0));
        var toPlace = Matrix4.Translation(new Vector3(origin.X + placed.Position.X, origin.Y + placed.Position.Y, 0));
        var transform = toPlace * compensate * rotation * toOrigin;
        foreach (var entity in sourceDoc.Entities.All)
        {
            var clone = (EntityObject)entity.Clone();
            clone.TransformBy(transform);
            clone.Layer = new Layer("Parts") { Color = AciColor.Default };
            targetDoc.Entities.Add(clone);
        }
    }

    private static Vector2 GetRotatedBoundsMin(NStCut.Core.Geometry.Rect sourceBounds, double rotation) => rotation switch
    {
        0 => new Vector2(0, 0), 90 => new Vector2(-sourceBounds.Height, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Only 0 and 90 degree rotations are supported.")
    };
}
