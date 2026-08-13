using System.IO;
using System.Text;
using NStCut.Core.Models;

namespace NStCut.App.Services;

public sealed class FileResolveService
{
    public IReadOnlyList<string> GetCandidates(string partName, string searchPath)
    {
        if (!Directory.Exists(searchPath) || string.IsNullOrWhiteSpace(partName))
            return Array.Empty<string>();

        var expectedFull = NameNormalizer.Normalize(partName);
        var expectedPrefix = NameNormalizer.FirstToken(partName);

        var allFiles = Directory
            .EnumerateFiles(searchPath, "*.dxf", SearchOption.AllDirectories)
            .ToList();

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

    public ResolveResult Resolve(string partName, string searchPath)
    {
        var candidates = GetCandidates(partName, searchPath);
        return candidates.Count switch
        {
            0 => new ResolveResult(PartStatus.NotFound, string.Empty, candidates),
            1 => new ResolveResult(PartStatus.Found, candidates[0], candidates),
            _ => new ResolveResult(PartStatus.Ambiguous, string.Empty, candidates)
        };
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
