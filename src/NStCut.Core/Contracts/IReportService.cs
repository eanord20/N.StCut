using System.Threading;
using NStCut.Core.Models;

namespace NStCut.Core.Contracts;

public sealed record ReportLabels(
    string DirectionHeader, string NoGrain, string Horizontal, string Vertical,
    string Generated = "Generated",
    string MaterialsHeader = "Materials",
    string MaterialHeader = "Material",
    string SizeHeader = "Size",
    string SheetsHeader = "Sheets",
    string AvgUtilizationHeader = "Avg. utilization %",
    string PartsHeader = "Parts",
    string PartHeader = "Part",
    string QtyHeader = "Qty",
    string PerimeterHeader = "Perimeter, mm",
    string UtilizationHeader = "Utilization %",
    string WasteAreaHeader = "Waste area",
    string NumberHeader = "Number",
    string SizeMmHeader = "Size, mm",
    string AreaMm2Header = "Area, mm²",
    string AreaM2Header = "Area, m²",
    string SheetLabel = "Sheet");

/// <summary>Опции содержимого отчёта (адаптировано из N.StLine ReportSettings).</summary>
public sealed record ReportContentOptions(
    string? Title,
    bool TitleIncludeDate,
    bool TitleIncludeTime,
    bool ShowSummary,
    bool IncludeMaterial,
    bool ShowPartNames,
    bool ShowArea = true,
    bool AreaInSquareMeters = false);

public interface IReportService
{
    void GenerateReport(
        string outputPath,
        string projectName,
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials,
        IReadOnlyList<SheetInstance> sheets,
        IReadOnlyList<byte[]> sheetThumbnails,
        ReportLabels? labels = null,
        ReportContentOptions? options = null,
        CancellationToken cancellationToken = default);
}
