using System.Threading;
using NStCut.Core.Models;

namespace NStCut.Core.Contracts;

public sealed record DxfExportLabels(string Sheet, string NoGrain, string Horizontal, string Vertical, string Material = "Material");

public interface IDxfExportService
{
    void WriteSheet(string outputPath, SheetInstance sheet, double partGapMm, double sheetMarginMm, DxfExportLabels? labels = null, CancellationToken cancellationToken = default);

    void WriteSheets(
        IReadOnlyList<(string OutputPath, SheetInstance Sheet)> sheets,
        double partGapMm,
        double sheetMarginMm,
        DxfExportLabels? labels = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in sheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteSheet(item.OutputPath, item.Sheet, partGapMm, sheetMarginMm, labels, cancellationToken);
        }
    }

    void WriteCombined(
        string outputPath,
        IReadOnlyList<SheetInstance> sheets,
        double partGapMm,
        double sheetMarginMm,
        DxfExportLabels labels,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
