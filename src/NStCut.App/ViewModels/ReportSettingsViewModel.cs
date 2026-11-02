using System.ComponentModel;
using System.Runtime.CompilerServices;
using NStCut.App.Formatters;
using NStCut.App.Models;

namespace NStCut.App.ViewModels;

/// <summary>Редактор настроек отчёта: работает с копией, OK в окне применяет Settings.</summary>
public sealed class ReportSettingsViewModel : INotifyPropertyChanged
{
    private readonly ReportSettings _settings = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReportSettingsViewModel(ReportSettings source, bool exportCombinedDxf)
    {
        _settings = source with { };
        ExportCombinedDxf = exportCombinedDxf;
    }

    public ReportSettings Settings => _settings;

    public ReportTimestampPosition TimestampPosition
    {
        get => _settings.TimestampPosition;
        set { _settings.TimestampPosition = value; Changed(); }
    }

    public bool FileNameIncludeDate
    {
        get => _settings.FileNameIncludeDate;
        set { _settings.FileNameIncludeDate = value; Changed(); }
    }

    public bool FileNameIncludeTime
    {
        get => _settings.FileNameIncludeTime;
        set { _settings.FileNameIncludeTime = value; Changed(); }
    }

    public string Title
    {
        get => _settings.Title;
        set { _settings.Title = value ?? string.Empty; Changed(); }
    }

    public bool TitleIncludeDate
    {
        get => _settings.TitleIncludeDate;
        set { _settings.TitleIncludeDate = value; Changed(); }
    }

    public bool TitleIncludeTime
    {
        get => _settings.TitleIncludeTime;
        set { _settings.TitleIncludeTime = value; Changed(); }
    }

    public bool ShowSummary
    {
        get => _settings.ShowSummary;
        set { _settings.ShowSummary = value; Changed(); }
    }

    public bool IncludeMaterial
    {
        get => _settings.IncludeMaterial;
        set { _settings.IncludeMaterial = value; Changed(); }
    }

    public bool ShowPartNames
    {
        get => _settings.ShowPartNames;
        set { _settings.ShowPartNames = value; Changed(); }
    }

    public bool ShowArea
    {
        get => _settings.ShowArea;
        set { _settings.ShowArea = value; Changed(); }
    }

    public ReportAreaUnit AreaUnit
    {
        get => _settings.AreaUnit;
        set { _settings.AreaUnit = value; Changed(); }
    }

    /// <summary>Экспорт DXF общим файлом — хранится вне ReportSettings (это настройка экспорта, не отчёта).</summary>
    public bool ExportCombinedDxf { get; set; }

    public string PreviewFileName
    {
        get
        {
            try
            {
                return ReportFileNameFormatter.Format(_settings with { FileName = string.IsNullOrWhiteSpace(_settings.FileName) ? "report" : _settings.FileName }, DateTime.Now);
            }
            catch
            {
                return _settings.FileName;
            }
        }
    }

    private void Changed([CallerMemberName] string? name = null)
    {
        OnPropertyChanged(name);
        OnPropertyChanged(nameof(PreviewFileName));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
