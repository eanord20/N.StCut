using System.Globalization;
using NStCut.Core.Models;
using NStCut.Core.Services;

namespace NStCut.Core.Tests;

public sealed class ExportFileNameServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly ExportFileNameService _service = new();

    public ExportFileNameServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
            // ignored
        }
    }

    private static SheetInstance CreateSheet(string materialName, double length, double width, int index)
        => new()
        {
            Material = new SheetMaterial { Name = materialName, Length = length, Width = width },
            Index = index
        };

    [Fact]
    public void DxfName_ReplacesInvalidWindowsFileNameCharactersWithUnderscore()
    {
        var sheet = CreateSheet("Steel 1/2<\\>|:?*\"", 1000, 500, 1);
        var timestamp = new DateTime(2025, 4, 15);

        var path = _service.GetDxfExportPath(_tempDir, sheet, timestamp);
        var fileName = Path.GetFileName(path);

        Assert.Equal("Steel 1_2_________1000x500_Sheet-01_20250415.dxf", fileName);
    }

    [Fact]
    public void DxfName_FormatsDimensionsInvariantlyWithoutTrailingZeros()
    {
        var sheet = CreateSheet("Alum", 1234.5, 600, 3);
        var timestamp = new DateTime(2025, 4, 15);

        var path = _service.GetDxfExportPath(_tempDir, sheet, timestamp);
        var fileName = Path.GetFileName(path);

        Assert.Equal("Alum_1234.5x600_Sheet-03_20250415.dxf", fileName);
    }

    [Fact]
    public void DxfName_ExactShape_MaterialLengthxWidthSheetNumberDate()
    {
        var sheet = CreateSheet("Steel", 2000, 1000, 7);
        var timestamp = new DateTime(2025, 12, 31);

        var path = _service.GetDxfExportPath(_tempDir, sheet, timestamp);
        var fileName = Path.GetFileName(path);

        Assert.Equal("Steel_2000x1000_Sheet-07_20251231.dxf", fileName);
    }

    [Fact]
    public void ReportName_ExactShape_DateTime()
    {
        var timestamp = new DateTime(2025, 4, 15, 9, 5, 42);

        var path = _service.GetReportExportPath(_tempDir, timestamp);
        var fileName = Path.GetFileName(path);

        Assert.Equal("20250415_090542.docx", fileName);
    }

    [Fact]
    public void DxfName_EmptyMaterialName_FallsBackToDefault()
    {
        var sheet = CreateSheet("   ", 1000, 500, 1);
        var timestamp = new DateTime(2025, 4, 15);

        var path = _service.GetDxfExportPath(_tempDir, sheet, timestamp);
        var fileName = Path.GetFileName(path);

        Assert.Equal("Material_1000x500_Sheet-01_20250415.dxf", fileName);
    }

    [Fact]
    public void DxfName_TrailingDotAndSpaceAreTrimmed()
    {
        var sheet = CreateSheet("Steel . ", 1000, 500, 1);
        var timestamp = new DateTime(2025, 4, 15);

        var path = _service.GetDxfExportPath(_tempDir, sheet, timestamp);
        var fileName = Path.GetFileName(path);

        Assert.Equal("Steel_1000x500_Sheet-01_20250415.dxf", fileName);
    }

    [Fact]
    public void DxfName_Collision_PicksFirstAvailableSuffix()
    {
        var sheet = CreateSheet("Steel", 1000, 500, 1);
        var timestamp = new DateTime(2025, 4, 15);

        File.WriteAllText(Path.Combine(_tempDir, "Steel_1000x500_Sheet-01_20250415.dxf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "Steel_1000x500_Sheet-01_20250415_1.dxf"), "");

        var path = _service.GetDxfExportPath(_tempDir, sheet, timestamp);
        var fileName = Path.GetFileName(path);

        Assert.Equal("Steel_1000x500_Sheet-01_20250415_2.dxf", fileName);
    }

    [Fact]
    public void ReportName_Collision_PicksFirstAvailableSuffix()
    {
        var timestamp = new DateTime(2025, 4, 15, 9, 5, 42);

        File.WriteAllText(Path.Combine(_tempDir, "20250415_090542.docx"), "");

        var path = _service.GetReportExportPath(_tempDir, timestamp);
        var fileName = Path.GetFileName(path);

        Assert.Equal("20250415_090542_1.docx", fileName);
    }

    [Fact]
    public void CombinedDxfName_UsesExactReportTimestampAndUniqueSuffix()
    {
        var timestamp = new DateTime(2025, 4, 15, 9, 5, 42);
        File.WriteAllText(Path.Combine(_tempDir, "20250415_090542.dxf"), "");

        var path = _service.GetCombinedDxfExportPath(_tempDir, timestamp);

        Assert.Equal("20250415_090542_1.dxf", Path.GetFileName(path));
    }

    [Fact]
    public void NumericFormatting_DoesNotDependOnCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            var sheet = CreateSheet("M", 1234.5, 600, 1);
            var timestamp = new DateTime(2025, 4, 15);

            var path = _service.GetDxfExportPath(_tempDir, sheet, timestamp);
            var fileName = Path.GetFileName(path);

            Assert.Equal("M_1234.5x600_Sheet-01_20250415.dxf", fileName);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
