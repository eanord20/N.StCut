using NStCut.Core.Constants;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Dxf.Services;
using NStCut.Nesting.Services;
using NStCut.Reports.Services;
using SkiaSharp;

namespace NStCut.IntegrationTests;

public class EndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public EndToEndTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void FullPipeline_CreatesDxfAndDocx()
    {
        // Arrange
        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 100 };
        var partPath = Path.Combine(_tempDir, "part.dxf");
        CreateRectangleDxf(partPath, 30, 30);

        var parts = new List<Part>
        {
            new() { Name = "P1", Quantity = 4, MaterialId = material.Id, DxfPath = partPath, Bounds = new Rect(0, 0, 30, 30), Area = 900, GrainMode = GrainMode.Allow90 }
        };

        var geometry = new DxfGeometryService();
        foreach (var part in parts)
        {
            var contour = geometry.ReadContour(part.DxfPath);
            part.Bounds = contour.Bounds;
            part.Area = contour.Bounds.Area;
        }

        var engine = new BoundingBoxShelfEngine();
        var result = engine.Nest(parts, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);

        Assert.NotEmpty(result.Sheets);
        Assert.Empty(result.Unplaced);

        var dxfOut = Path.Combine(_tempDir, "sheet.dxf");
        var dxfExport = new DxfExportService();
        dxfExport.WriteSheet(dxfOut, result.Sheets[0], 5.0, 5.0);
        Assert.True(File.Exists(dxfOut));
        Assert.True(new FileInfo(dxfOut).Length > 0);

        var reportOut = Path.Combine(_tempDir, "report.docx");
        var thumbnails = result.Sheets.Select(_ => CreateMinimalPng()).ToList();
        var report = new ReportService();
        report.GenerateReport(reportOut, "Test", parts, result.Sheets, thumbnails);
        Assert.True(File.Exists(reportOut));
        Assert.True(new FileInfo(reportOut).Length > 0);
    }

    private static void CreateRectangleDxf(string path, double w, double h)
    {
        var doc = new netDxf.DxfDocument();
        var rect = new netDxf.Entities.Polyline2D(new[]
        {
            new netDxf.Vector2(0, 0),
            new netDxf.Vector2(w, 0),
            new netDxf.Vector2(w, h),
            new netDxf.Vector2(0, h)
        }, true);
        doc.Entities.Add(rect);
        doc.Save(path);
    }

    private static byte[] CreateMinimalPng()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
