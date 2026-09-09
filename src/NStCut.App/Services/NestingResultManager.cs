using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using NStCut.Core.Contracts;
using NStCut.Core.Models;

namespace NStCut.App.Services;

/// <summary>
/// Default implementation of <see cref="INestingResultManager"/>. Centralizes the current
/// nesting result, last report path and derived command-state without involving UI code.
/// </summary>
public sealed class NestingResultManager : INestingResultManager
{
    private NestingResult? _current;
    private string? _lastReportPath;

    public NestingResult? Current
    {
        get => _current;
        private set
        {
            _current = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasResult));
        }
    }

    public bool HasResult => _current != null;

    public string? LastReportPath
    {
        get => _lastReportPath;
        private set
        {
            _lastReportPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLastReportPath));
        }
    }

    public bool HasLastReportPath =>
        !string.IsNullOrWhiteSpace(_lastReportPath) && File.Exists(_lastReportPath);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Set(NestingResult? result)
        => Current = result;

    public void Clear()
        => Current = null;

    public void SetLastReportPath(string? path)
        => LastReportPath = path;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
