using NStCut.Core.Models;

namespace NStCut.Core.Contracts;

public interface IExportFileNameService
{
    DateTime GetBatchTimestamp();

    string GetDxfExportPath(string outputDirectory, SheetInstance sheet);
    string GetDxfExportPath(string outputDirectory, SheetInstance sheet, DateTime timestamp);

    string GetCombinedDxfExportPath(string outputDirectory)
        => GetCombinedDxfExportPath(outputDirectory, GetBatchTimestamp());
    string GetCombinedDxfExportPath(string outputDirectory, DateTime timestamp)
        => throw new NotSupportedException();

    string GetReportExportPath(string outputDirectory);
    string GetReportExportPath(string outputDirectory, DateTime timestamp);
}
