namespace NStCut.App.Services;

/// <summary>Абстракция над FileResolveService — нужна для DIP/тестируемости MainViewModel (см. docs/Audit_Architecture.md, SOLID Assessment, D).</summary>
public interface IFileResolveService
{
    IReadOnlyList<string> GetCandidates(string partName, string searchPath, string? excludePath = null);
    IReadOnlyList<string> EnumerateDxfFiles(string searchPath, string? excludePath = null);
    IReadOnlyList<string> GetCandidates(string partName, IReadOnlyList<string> allFiles);
    ResolveResult Resolve(string partName, string searchPath, string? excludePath = null);
    ResolveResult Resolve(string partName, IReadOnlyList<string> allFiles);
}
