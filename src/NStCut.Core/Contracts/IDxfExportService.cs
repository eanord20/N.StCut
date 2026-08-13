using NStCut.Core.Models;

namespace NStCut.Core.Contracts;

public interface IDxfExportService
{
    void WriteSheet(string outputPath, SheetInstance sheet, double partGapMm, double sheetMarginMm);
}
