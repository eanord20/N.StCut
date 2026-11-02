using System.Globalization;
using NStCut.Core.Constants;
using NStCut.Core.Contracts;
using NStCut.Core.Models;

namespace NStCut.Core.Services;

public sealed class ExportFileNameService : IExportFileNameService
{
    public DateTime GetBatchTimestamp() => DateTime.Now;

    public string GetDxfExportPath(string outputDirectory, SheetInstance sheet)
        => GetDxfExportPath(outputDirectory, sheet, GetBatchTimestamp());

    public string GetDxfExportPath(string outputDirectory, SheetInstance sheet, DateTime timestamp)
    {
        var baseName = BuildDxfBaseName(sheet, timestamp);
        return GetUniquePath(outputDirectory, baseName, ExportFileNameDefaults.DxfExtension);
    }

    public string GetCombinedDxfExportPath(string outputDirectory)
        => GetCombinedDxfExportPath(outputDirectory, GetBatchTimestamp());

    public string GetCombinedDxfExportPath(string outputDirectory, DateTime timestamp)
        => GetUniquePath(outputDirectory,
            timestamp.ToString(ExportFileNameDefaults.ReportDateTimeFormat),
            ExportFileNameDefaults.DxfExtension);

    public string GetReportExportPath(string outputDirectory)
        => GetReportExportPath(outputDirectory, GetBatchTimestamp());

    public string GetReportExportPath(string outputDirectory, DateTime timestamp)
    {
        var baseName = timestamp.ToString(ExportFileNameDefaults.ReportDateTimeFormat);
        return GetUniquePath(outputDirectory, baseName, ExportFileNameDefaults.ReportExtension);
    }

    private static string BuildDxfBaseName(SheetInstance sheet, DateTime timestamp)
    {
        var material = Sanitize(sheet.Material.Name);
        var length = FormatDimension(sheet.Material.Length);
        var width = FormatDimension(sheet.Material.Width);
        var date = timestamp.ToString(ExportFileNameDefaults.DxfDateFormat);
        return $"{material}_{length}x{width}_{ExportFileNameDefaults.SheetPrefix}{sheet.Index:D2}_{date}";
    }

    private static string GetUniquePath(string directory, string baseName, string extension)
    {
        Directory.CreateDirectory(directory);
        var candidate = Path.Combine(directory, baseName + extension);
        if (!File.Exists(candidate))
            return candidate;

        for (var i = 1; ; i++)
        {
            candidate = Path.Combine(directory, $"{baseName}_{i}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static string FormatDimension(double value)
        => value.ToString(ExportFileNameDefaults.InvariantDecimalFormat, CultureInfo.InvariantCulture);

    private static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ExportFileNameDefaults.DefaultMaterialName;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name!.Select(c => Array.IndexOf(invalidChars, c) >= 0 ? '_' : c).ToArray());
        sanitized = sanitized.TrimEnd(' ', '.');

        return string.IsNullOrWhiteSpace(sanitized)
            ? ExportFileNameDefaults.DefaultMaterialName
            : sanitized;
    }
}
