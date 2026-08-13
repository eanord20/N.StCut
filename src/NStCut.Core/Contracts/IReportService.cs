using NStCut.Core.Models;

namespace NStCut.Core.Contracts;

public interface IReportService
{
    void GenerateReport(
        string outputPath,
        string projectName,
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetInstance> sheets,
        IReadOnlyList<byte[]> sheetThumbnails);
}
