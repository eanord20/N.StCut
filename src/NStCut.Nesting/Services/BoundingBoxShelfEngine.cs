using NStCut.Core.Contracts;
using NStCut.Core.Models;
using NStCut.Nesting.Internals;

namespace NStCut.Nesting.Services;

internal enum PackingStrategy
{
    MaxSide,
    Area,
    Width,
    Height
}

public sealed class BoundingBoxShelfEngine : INestingEngine
{
    private sealed record GroupCandidate(List<SheetInstance> Sheets, List<Part> Unplaced, double Utilization);

    public NestingResult Nest(
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials,
        NestingOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return NestCore(parts, materials, options, Enum.GetValues<PackingStrategy>(), progress, cancellationToken);
    }

    internal NestingResult NestWithStrategy(
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials,
        NestingOptions options,
        PackingStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        return NestCore(parts, materials, options, new[] { strategy }, null, cancellationToken);
    }

    private static NestingResult NestCore(
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials,
        NestingOptions options,
        IReadOnlyList<PackingStrategy> strategies,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var result = new NestingResult();
        var materialDict = materials.ToDictionary(m => m.Id);
        var expanded = parts
            .SelectMany((part, stableIndex) => Enumerable.Range(1, Math.Max(0, part.Quantity))
                .Select(copyIndex => (CopyIndex: copyIndex, Part: part, StableIndex: stableIndex)))
            .ToList();
        var groups = expanded.GroupBy(x => x.Part.MaterialId);
        var processed = 0;
        var totalParts = expanded.Count;

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groupCopies = group.ToList();
            if (!materialDict.TryGetValue(group.Key, out var material))
            {
                foreach (var item in groupCopies)
                    result.Unplaced.Add(CloneForUnplaced(item.Part));
                processed += groupCopies.Count;
                ReportProgress(progress, processed, totalParts);
                continue;
            }

            GroupCandidate? best = null;
            foreach (var strategy in strategies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = PackGroup(groupCopies, material, options, strategy, cancellationToken);
                if (best is null || IsBetter(candidate, best))
                    best = candidate;
            }

            result.Sheets.AddRange(best!.Sheets);
            result.Unplaced.AddRange(best.Unplaced);
            processed += groupCopies.Count;
            ReportProgress(progress, processed, totalParts);
        }

        var totalSheetArea = result.Sheets.Sum(sheet => sheet.Material.Length * sheet.Material.Width);
        var totalPlacedArea = result.Sheets.Sum(sheet => sheet.Parts.Sum(part => part.Bounds.Area));
        result.TotalUtilizationPercent = totalSheetArea > 0
            ? totalPlacedArea / totalSheetArea * 100.0
            : 0.0;
        return result;
    }

    private static GroupCandidate PackGroup(
        List<(int CopyIndex, Part Part, int StableIndex)> copies,
        SheetMaterial material,
        NestingOptions options,
        PackingStrategy strategy,
        CancellationToken cancellationToken)
    {
        var ordered = strategy switch
        {
            PackingStrategy.MaxSide => copies.OrderByDescending(x => Math.Max(x.Part.Bounds.Width, x.Part.Bounds.Height)),
            PackingStrategy.Area => copies.OrderByDescending(x => x.Part.Bounds.Area),
            PackingStrategy.Width => copies.OrderByDescending(x => x.Part.Bounds.Width),
            PackingStrategy.Height => copies.OrderByDescending(x => x.Part.Bounds.Height),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };
        var sorted = ordered.ThenBy(x => x.StableIndex).ThenBy(x => x.CopyIndex).ToList();
        var packers = new List<MaxRectsPacker>();
        var unplaced = new List<Part>();

        foreach (var (copyIndex, part, _) in sorted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var placed = packers.Any(packer => packer.TryPlace(part, copyIndex));
            if (placed)
                continue;

            var newPacker = CreatePacker(material, options);
            if (newPacker.TryPlace(part, copyIndex))
                packers.Add(newPacker);
            else
                unplaced.Add(CloneForUnplaced(part));
        }

        var sheets = packers.Where(p => p.Parts.Count > 0)
            .Select((packer, index) => packer.ToSheet(index + 1))
            .ToList();
        var sheetArea = material.Length * material.Width;
        var utilization = sheets.Count > 0 && sheetArea > 0
            ? sheets.Sum(s => s.Parts.Sum(p => p.Bounds.Area)) / (sheetArea * sheets.Count)
            : 0;
        return new GroupCandidate(sheets, unplaced, utilization);
    }

    private static bool IsBetter(GroupCandidate candidate, GroupCandidate current)
    {
        if (candidate.Unplaced.Count != current.Unplaced.Count)
            return candidate.Unplaced.Count < current.Unplaced.Count;
        if (candidate.Sheets.Count != current.Sheets.Count)
            return candidate.Sheets.Count < current.Sheets.Count;
        return candidate.Utilization > current.Utilization;
    }

    private static MaxRectsPacker CreatePacker(SheetMaterial material, NestingOptions options) =>
        new(material, options.SheetMarginMm, options.PartGapMm, options.Resolution,
            options.PlacementHeuristic, options.OrientationPreference);

    private static void ReportProgress(IProgress<double>? progress, int processed, int totalParts)
    {
        if (totalParts > 0)
            progress?.Report(processed / (double)totalParts * 100.0);
    }

    private static Part CloneForUnplaced(Part part)
    {
        var clone = part.Clone();
        clone.Quantity = 1;
        return clone;
    }
}
