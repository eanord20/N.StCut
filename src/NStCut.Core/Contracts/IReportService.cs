using System.Threading;
using NStCut.Core.Models;

namespace NStCut.Core.Contracts;

public sealed record ReportLabels(string DirectionHeader, string NoGrain, string Horizontal, string Vertical);

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
        CancellationToken cancellationToken = default);
}
