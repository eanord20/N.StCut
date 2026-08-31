namespace NStCut.App.Models;

/// <summary>Настройки приложения, которые должны сохраняться между запусками (не привязаны к конкретному проекту).</summary>
public sealed class AppSettings
{
    public string SearchPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public double SheetMarginMm { get; set; }
    public double PartGapMm { get; set; }
    public int MaxSearchPasses { get; set; }
    public int NoImprovementStopCount { get; set; }
    public int AcceptableMaxUnplaced { get; set; }
    public int AcceptableMaxSheets { get; set; }
    public string Culture { get; set; } = "en";
    /// <summary>"Light" или "Dark" — см. NStCut.App.Services.AppTheme.</summary>
    public string Theme { get; set; } = "Light";

    /// <summary>true — экспорт DXF пишет один общий файл с презентацией; false — отдельный файл на каждый лист.</summary>
    public bool ExportCombinedDxf { get; set; }

    /// <summary>
    /// Пользовательские ширины столбцов таблицы деталей (MainWindow.PartsGrid), по имени колонки
    /// (x:Name в MainWindow.xaml, например "ColNumber"). Сохраняется при закрытии окна, чтобы
    /// ручное изменение ширины столбцов не сбрасывалось между запусками.
    /// </summary>
    public Dictionary<string, double> PartsGridColumnWidths { get; set; } = new();
}
