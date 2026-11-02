using System.Globalization;
using System.IO;
using NStCut.App.Models;
using NStCut.App.Services;
using NStCut.Core.Constants;

namespace NStCut.App.Formatters;

/// <summary>Именование отчёта .docx — адаптация N.StLine ReportFileNameFormatter.</summary>
public static class ReportFileNameFormatter
{
    public static string Format(ReportSettings settings, DateTime timestamp)
    {
        var ext = ExportFileNameDefaults.ReportExtension;
        var name = string.IsNullOrWhiteSpace(settings.FileName)
            ? timestamp.ToString(ExportFileNameDefaults.ReportDateTimeFormat, CultureInfo.InvariantCulture)
            : settings.FileName.Trim();

        Validate(name);
        if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            name = name[..^ext.Length];
        Validate(name);

        var stamp = FormatTimestamp(timestamp, settings.FileNameIncludeDate, settings.FileNameIncludeTime,
            ExportFileNameDefaults.ReportDateFormat, ExportFileNameDefaults.ReportTimeFormat, "_");
        if (stamp.Length > 0 && !string.IsNullOrWhiteSpace(settings.FileName))
            name = settings.TimestampPosition switch
            {
                ReportTimestampPosition.Prefix => $"{stamp}_{name}",
                ReportTimestampPosition.Suffix => $"{name}_{stamp}",
                _ => name
            };
        name += ext;
        Validate(name);
        return name;
    }

    public static string FormatTimestamp(DateTime timestamp, bool date, bool time,
        string dateFormat, string timeFormat, string separator)
        => string.Join(separator, new[]
        {
            date ? timestamp.ToString(dateFormat, CultureInfo.InvariantCulture) : null,
            time ? timestamp.ToString(timeFormat, CultureInfo.InvariantCulture) : null
        }.Where(value => value != null));

    private static void Validate(string name)
    {
        var stem = name.Split('.')[0].TrimEnd().ToUpperInvariant();
        var reserved = stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
             (stem[3] is >= '1' and <= '9' or '¹' or '²' or '³'));
        if (string.IsNullOrWhiteSpace(name) || name.Length > ExportFileNameDefaults.ReportMaxFileNameLength ||
            name.EndsWith('.') || name.EndsWith(' ') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || reserved)
            throw new ArgumentException(LocalizationService.Instance.ErrorInvalidReportFileName);
    }
}
