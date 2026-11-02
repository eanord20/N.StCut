using System.IO;
using System.Text;
using NStCut.Core.Models;

namespace NStCut.App.Services;

public sealed class FileResolveService : IFileResolveService
{
    /// <summary>
    /// Ищет DXF-файлы, соответствующие имени детали, в папке поиска.
    /// </summary>
    /// <param name="partName">Отображаемое имя детали ("Номер Название").</param>
    /// <param name="searchPath">Папка поиска (рекурсивно).</param>
    /// <param name="excludePath">
    /// Папка, которую нужно исключить из поиска (обычно — папка сохранения/экспорта проекта).
    /// Если она вложена в <paramref name="searchPath"/> или совпадает с ней, ранее экспортированные
    /// файлы не будут попадать в кандидаты и приводить к ложному статусу Ambiguous.
    /// </param>
    public IReadOnlyList<string> GetCandidates(string partName, string searchPath, string? excludePath = null)
    {
        if (!Directory.Exists(searchPath) || string.IsNullOrWhiteSpace(partName))
            return Array.Empty<string>();

        return GetCandidates(partName, EnumerateDxfFiles(searchPath, excludePath));
    }

    /// <summary>
    /// Перечисляет все DXF-файлы в папке поиска один раз (за вычетом <paramref name="excludePath"/>).
    /// Используется при пакетном разрешении статуса для всех строк таблицы (см. MainViewModel.FindFiles),
    /// чтобы не сканировать диск заново на каждую деталь (docs/Audit_Performance.md, issue P1).
    /// </summary>
    public IReadOnlyList<string> EnumerateDxfFiles(string searchPath, string? excludePath = null)
    {
        if (!Directory.Exists(searchPath))
            return Array.Empty<string>();

        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(searchPath);

        // Ручной обход: SearchOption.AllDirectories бросает UnauthorizedAccessException
        // на первой же недоступной папке (например, "System Volume Information" в корне диска).
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFiles(dir, "*.dxf").ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            files.AddRange(entries);

            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(dir).ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var sub in subdirs)
            {
                if (!IsUnderExcludedPath(sub, excludePath))
                    pending.Push(sub);
            }
        }

        return files
            .Where(path => !IsUnderExcludedPath(path, excludePath))
            .ToList();
    }

    /// <summary>Вариант GetCandidates, принимающий уже перечисленный список файлов (без повторного сканирования диска).</summary>
    public IReadOnlyList<string> GetCandidates(string partName, IReadOnlyList<string> allFiles)
    {
        if (string.IsNullOrWhiteSpace(partName))
            return Array.Empty<string>();

        var expectedFull = NameNormalizer.Normalize(partName);
        var expectedPrefix = NameNormalizer.FirstToken(partName);

        // 1) exact normalized match
        var candidates = allFiles
            .Where(path => NameNormalizer.Normalize(Path.GetFileNameWithoutExtension(path)) == expectedFull)
            .ToList();

        if (candidates.Count > 0)
            return candidates;

        // 2) fallback: first token of part name is prefix of file name
        if (!string.IsNullOrWhiteSpace(expectedPrefix))
        {
            candidates = allFiles
                .Where(path => NameNormalizer.Normalize(Path.GetFileNameWithoutExtension(path))
                    .StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return candidates;
    }

    public ResolveResult Resolve(string partName, string searchPath, string? excludePath = null)
        => ToResult(GetCandidates(partName, searchPath, excludePath));

    /// <summary>Вариант Resolve, принимающий уже перечисленный список файлов (без повторного сканирования диска).</summary>
    public ResolveResult Resolve(string partName, IReadOnlyList<string> allFiles)
        => ToResult(GetCandidates(partName, allFiles));

    private static ResolveResult ToResult(IReadOnlyList<string> candidates) => candidates.Count switch
    {
        0 => new ResolveResult(PartStatus.NotFound, string.Empty, candidates),
        1 => new ResolveResult(PartStatus.Found, candidates[0], candidates),
        _ => new ResolveResult(PartStatus.Ambiguous, string.Empty, candidates)
    };

    private static bool IsUnderExcludedPath(string filePath, string? excludePath)
    {
        if (string.IsNullOrWhiteSpace(excludePath) || !Directory.Exists(excludePath))
            return false;

        var fullFile = Path.GetFullPath(filePath);
        var fullExclude = Path.GetFullPath(excludePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullFile.StartsWith(fullExclude + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullFile, fullExclude, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ResolveResult(PartStatus Status, string Path, IReadOnlyList<string> Candidates);

public static class NameNormalizer
{
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder();
        bool prevWasSpace = false;

        foreach (var ch in input.Trim().ToLowerInvariant())
        {
            if (IsForbiddenWindowsChar(ch))
                continue;

            if (ch == '_' || ch == '-' || char.IsWhiteSpace(ch))
            {
                if (!prevWasSpace)
                {
                    sb.Append(' ');
                    prevWasSpace = true;
                }
                continue;
            }

            sb.Append(ch);
            prevWasSpace = false;
        }

        return sb.ToString().Trim();
    }

    public static string FirstToken(string input)
    {
        var normalized = Normalize(input);
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    private static bool IsForbiddenWindowsChar(char ch)
        => ch is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*';
}
