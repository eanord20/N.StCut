using System.Buffers.Binary;
using System.Text;
using NStCut.Core.Constants;
using NStCut.Core.Contracts;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Reports.Services;
using SkiaSharp;
using Xceed.Words.NET;

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
        var part = new Part { Number = "P1", Quantity = 1, MaterialId = material.Id, Area = 5000, Bounds = new Rect(0, 0, 50, 100) };

        var png = CreateMinimalPng();
        var service = new ReportService();
        service.GenerateReport(path, "Test Project", new List<Part> { part }, new List<SheetMaterial> { material },
            new List<SheetInstance> { sheet }, new List<byte[]> { png });

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Theory]
    [InlineData(GrainDirection.Horizontal, "Горизонтальна")]
    [InlineData(GrainDirection.Vertical, "Вертикальна")]
    public void GenerateReport_UsesLocalizedDirectionLabels(GrainDirection direction, string localizedLabel)
    {
        var path = Path.Combine(_tempDir, $"localized-{direction}-{localizedLabel}.docx");
        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 100, HasGrain = true, GrainDirection = direction };
        var sheet = new SheetInstance { Material = material, Index = 1, UtilizationPercent = 50, WasteArea = 5000 };
        var labels = new ReportLabels("Напрямок", "Без текстури", "Горизонтальна", "Вертикальна");

        var png = CreateMinimalPng();
        new ReportService().GenerateReport(path, "Test", new List<Part>(), new List<SheetMaterial> { material },
            new List<SheetInstance> { sheet }, new List<byte[]> { png }, labels);

        using var doc = DocX.Load(path);
        var tableText = string.Join("|", doc.Tables.SelectMany(t => t.Rows)
            .SelectMany(r => r.Cells)
            .Select(c => c.Paragraphs.FirstOrDefault()?.Text ?? string.Empty));

        Assert.Contains(localizedLabel, tableText);
        Assert.Contains(labels.DirectionHeader, tableText);
    }

    [Fact]
    public void GenerateReport_ThumbnailPreservesAspectRatioWithinBounds()
    {
        var path = Path.Combine(_tempDir, "thumbnail.docx");
        var material = new SheetMaterial { Name = "Steel", Length = 400, Width = 100 };
        var sheet = new SheetInstance { Material = material, Index = 1, UtilizationPercent = 50, WasteArea = 5000 };
        var png = CreatePng(800, 200);

        new ReportService().GenerateReport(path, "Test", new List<Part>(), new List<SheetMaterial> { material },
            new List<SheetInstance> { sheet }, new List<byte[]> { png });

        using var doc = DocX.Load(path);
        var picture = doc.Pictures.First();

        Assert.True(picture.Width <= 400);
        Assert.True(picture.Height <= 300);
        Assert.Equal(4.0, (double)picture.Width / picture.Height, 1);
    }

    [Theory]
    [InlineData(800, 200, 400, 100)]
    [InlineData(100, 100, 100, 100)]
    [InlineData(600, 400, 400, 266)]
    public void GetThumbnailSize_PreservesAspectRatio(int originalWidth, int originalHeight, int expectedWidth, int expectedHeight)
    {
        var png = CreatePngHeader(originalWidth, originalHeight);

        var (width, height) = ReportService.GetThumbnailSize(png);

        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    [Fact]
    public void GetThumbnailSize_NonPngBytes_FallsBackToMax()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var (width, height) = ReportService.GetThumbnailSize(bytes);

        Assert.Equal(ExportPresentationConstants.MaxReportThumbnailWidth, width);
        Assert.Equal(ExportPresentationConstants.MaxReportThumbnailHeight, height);
    }

    [Fact]
    public void GetThumbnailSize_TruncatedPng_FallsBackToMax()
    {
        var png = CreatePngHeader(100, 100);
        var truncated = png.Take(20).ToArray();

        var (width, height) = ReportService.GetThumbnailSize(truncated);

        Assert.Equal(ExportPresentationConstants.MaxReportThumbnailWidth, width);
        Assert.Equal(ExportPresentationConstants.MaxReportThumbnailHeight, height);
    }

    [Fact]
    public void GetThumbnailSize_ZeroDimensions_FallsBackToMax()
    {
        var png = CreatePngHeader(0, 0);

        var (width, height) = ReportService.GetThumbnailSize(png);

        Assert.Equal(ExportPresentationConstants.MaxReportThumbnailWidth, width);
        Assert.Equal(ExportPresentationConstants.MaxReportThumbnailHeight, height);
    }

    private static byte[] CreateMinimalPng()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] CreatePngHeader(int width, int height)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8;
        ihdr[9] = 0;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;

        var lengthBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, 13u);
        ms.Write(lengthBytes);

        ms.Write(Encoding.ASCII.GetBytes("IHDR"));
        ms.Write(ihdr);
        ms.Write(new byte[4]); // dummy CRC

        return ms.ToArray();
    }
}
