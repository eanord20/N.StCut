namespace NStCut.App.Models;

public enum ReportTimestampPosition { None, Prefix, Suffix }
public enum ReportAreaUnit { SquareMm, SquareM }

/// <summary>
/// Настройки генерации отчёта (.docx). Адаптировано из N.StLine.ReportSettings:
/// часть про 1D-разделители убрана, для листового раскроя не нужна.
/// </summary>
public sealed record ReportSettings
{
    public string FileName { get; set; } = string.Empty;
    public ReportTimestampPosition TimestampPosition { get; set; } = ReportTimestampPosition.Suffix;
    public bool FileNameIncludeDate { get; set; } = true;
    public bool FileNameIncludeTime { get; set; } = true;
    public string Title { get; set; } = string.Empty;
    public bool TitleIncludeDate { get; set; }
    public bool TitleIncludeTime { get; set; }
    public bool ShowSummary { get; set; } = true;
    public bool IncludeMaterial { get; set; } = true;
    public bool ShowPartNames { get; set; } = true;
    /// <summary>Показывать площадь деталей/отходов в таблицах отчёта.</summary>
    public bool ShowArea { get; set; } = true;
    /// <summary>Единицы площади: мм² или м².</summary>
    public ReportAreaUnit AreaUnit { get; set; }
}
