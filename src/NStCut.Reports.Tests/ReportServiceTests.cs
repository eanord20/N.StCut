using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Reports.Services;
using SkiaSharp;

namespace NStCut.Reports.Tests;

public class ReportServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public ReportServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void GenerateReport_CreatesDocxFile()
    {
        var path = Path.Combine(_tempDir, "report.docx");
        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 100 };
        var sheet = new SheetInstance { Material = material, Index = 1, UtilizationPercent = 50, WasteArea = 5000 };
        var part = new Part { Name = "P1", Quantity = 1, MaterialId = material.Id, Area = 5000, Bounds = new Rect(0, 0, 50, 100) };

        var png = CreateMinimalPng();
        var service = new ReportService();
        service.GenerateReport(path, "Test Project", new List<Part> { part }, new List<SheetInstance> { sheet }, new List<byte[]> { png });

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    private static byte[] CreateMinimalPng()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
