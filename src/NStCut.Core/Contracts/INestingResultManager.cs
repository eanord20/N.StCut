using System.ComponentModel;
using NStCut.Core.Models;

namespace NStCut.Core.Contracts;

/// <summary>
/// Holds the current <see cref="NestingResult"/>, tracks the last report path and exposes
/// derived state (HasResult, HasLastReportPath) for command enablement. Keeps the result
/// lifecycle out of the main ViewModel.
/// </summary>
public interface INestingResultManager : INotifyPropertyChanged
{
    NestingResult? Current { get; }
    bool HasResult { get; }

    string? LastReportPath { get; }
    bool HasLastReportPath { get; }

    void Set(NestingResult? result);
    void Clear();
    void SetLastReportPath(string? path);
}
