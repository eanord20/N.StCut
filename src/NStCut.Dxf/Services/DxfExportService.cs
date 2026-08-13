using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using NStCut.Core.Contracts;
using NStCut.Core.Models;

namespace NStCut.Dxf.Services;

public sealed class DxfExportService : IDxfExportService
{
    public void WriteSheet(string outputPath, SheetInstance sheet, double partGapMm, double sheetMarginMm)
    {
        var doc = new DxfDocument();

        // Sheet boundary rectangle
        var boundary = new Polyline2D(new[]
        {
            new Vector2(0, 0),
            new Vector2(sheet.Material.Length, 0),
            new Vector2(sheet.Material.Length, sheet.Material.Width),
            new Vector2(0, sheet.Material.Width)
        }, true);
        boundary.Layer = new Layer("SheetBoundary") { Color = AciColor.Yellow };
        doc.Entities.Add(boundary);

        foreach (var placed in sheet.Parts)
        {
            WritePlacedPart(doc, placed);
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        doc.Save(outputPath);
    }

    private static void WritePlacedPart(DxfDocument targetDoc, PlacedPart placed)
    {
        if (!File.Exists(placed.Source.DxfPath))
            return;

        var sourceDoc = DxfDocument.Load(placed.Source.DxfPath);
        var sourceBounds = placed.Source.Bounds;

        // Translate so the source bounding-box bottom-left lands at (0,0),
        // then rotate, then move to final position.
        // netDxf uses column vectors: M_final * v. Apply transforms right-to-left.
        var toOrigin = Matrix4.Translation(new Vector3(-sourceBounds.Left, -sourceBounds.Bottom, 0));
        var rotation = Matrix4.RotationZ(placed.Rotation * Math.PI / 180.0);
        var toPlace = Matrix4.Translation(new Vector3(placed.Position.X, placed.Position.Y, 0));
        var transform = toPlace * rotation * toOrigin;

        foreach (var entity in sourceDoc.Entities.All)
        {
            var clone = (EntityObject)entity.Clone();
            clone.TransformBy(transform);
            clone.Layer = new Layer("Parts") { Color = AciColor.Default };
            targetDoc.Entities.Add(clone);
        }
    }
}
