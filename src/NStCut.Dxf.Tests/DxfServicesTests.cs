using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Dxf.Services;

namespace NStCut.Dxf.Tests;

public class DxfServicesTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public DxfServicesTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void ReadContour_ReturnsBoundsFromRectanglePolyline()
    {
        var path = Path.Combine(_tempDir, "rect.dxf");
        CreateRectangleDxf(path, 10, 20);

        var service = new DxfGeometryService();
        var contour = service.ReadContour(path);

        Assert.Equal(10, contour.Bounds.Width);
        Assert.Equal(20, contour.Bounds.Height);
    }

    [Fact]
    public void WriteSheet_CreatesOutputFile()
    {
        var sourcePath = Path.Combine(_tempDir, "part.dxf");
        CreateRectangleDxf(sourcePath, 10, 10);

        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 50 };
        var sheet = new SheetInstance { Material = material, Index = 1 };
        sheet.Parts.Add(new PlacedPart
        {
            Source = new Part { Name = "P1", DxfPath = sourcePath, Bounds = new Rect(0, 0, 10, 10) },
            CopyIndex = 1,
            Position = new Point(5, 5),
            Rotation = 0,
            Bounds = new Rect(5, 5, 10, 10)
        });

        var export = new DxfExportService();
        var outPath = Path.Combine(_tempDir, "sheet.dxf");
        export.WriteSheet(outPath, sheet, 5, 5);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    private static void CreateRectangleDxf(string path, double width, double height)
    {
        var doc = new netDxf.DxfDocument();
        var rect = new netDxf.Entities.Polyline2D(new[]
        {
            new netDxf.Vector2(0, 0),
            new netDxf.Vector2(width, 0),
            new netDxf.Vector2(width, height),
            new netDxf.Vector2(0, height)
        }, true);
        doc.Entities.Add(rect);
        doc.Save(path);
    }
}
