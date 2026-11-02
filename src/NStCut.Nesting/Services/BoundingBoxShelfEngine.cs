using NStCut.Core.Contracts;
using NStCut.Core.Models;
using NStCut.Core.Nesting;
using NStCut.Nesting.Internals;

namespace NStCut.Nesting.Services;

internal enum PackingStrategy
{
    MaxSide,
    Area,
    Width,
    Height,
    AsGiven
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
        var values = Enum.GetValues<PackingStrategy>();
        var strategies = options.PackStrategyIndex >= 0 && options.PackStrategyIndex < values.Length
            ? new[] { values[options.PackStrategyIndex] }
            : (IReadOnlyList<PackingStrategy>)values;
        return NestCore(parts, materials, options, strategies, progress, cancellationToken);
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
        var materialDict = materials
            .GroupBy(m => m.Id)
            .ToDictionary(g => g.Key, g => g.First());
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

            var effectiveOptions = EffectiveOptions(options, material);
            GroupCandidate? best = null;
            foreach (var strategy in strategies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = PackGroup(groupCopies, material, effectiveOptions, strategy, cancellationToken);
                if (best is null || IsBetter(candidate, best, options))
                    best = candidate;
            }

            result.Sheets.AddRange(best!.Sheets);
            result.Unplaced.AddRange(best.Unplaced);
            processed += groupCopies.Count;
            ReportProgress(progress, processed, totalParts);
        }

        // Мягкий отступ: детали, не разместившиеся с заданным отступом, но
        // влезающие в физический габарит листа, раскладываются повторно без
        // отступа (на отдельных листах) и добавляются к результату.
        if (options.AllowMarginRelax && result.Unplaced.Count > 0)
        {
            var relaxedOptions = new NestingOptions
            {
                PartGapMm = options.PartGapMm,
                SheetMarginMm = 0,
                Resolution = options.Resolution,
                MaxSearchPasses = options.MaxSearchPasses,
                NoImprovementStopCount = options.NoImprovementStopCount,
                AcceptableMaxUnplaced = options.AcceptableMaxUnplaced,
                AcceptableMaxSheets = options.AcceptableMaxSheets,
                PlacementHeuristic = options.PlacementHeuristic,
                OrientationPreference = options.OrientationPreference,
                PackStrategyIndex = options.PackStrategyIndex,
                EnableGapFilling = options.EnableGapFilling
            };
            var relaxed = NestCore(result.Unplaced, materials, relaxedOptions, strategies, null, cancellationToken);
            if (relaxed.Sheets.Count > 0)
            {
                result.Sheets.AddRange(relaxed.Sheets);
                result.Unplaced.Clear();
                result.Unplaced.AddRange(relaxed.Unplaced);
            }
        }

        // Sheets are numbered per material group during packing; renumber
        // sequentially across the whole result so indices stay unique.
        for (var i = 0; i < result.Sheets.Count; i++)
            result.Sheets[i].Index = i + 1;

        var totalSheetArea = result.Sheets.Sum(sheet => sheet.Material.Length * sheet.Material.Width);
        var totalPlacedArea = result.Sheets.Sum(sheet => sheet.Parts.Sum(part => part.Source.TrueArea));
        result.TotalUtilizationPercent = totalSheetArea > 0
            ? totalPlacedArea / totalSheetArea * 100.0
            : 0.0;
        result.LayoutHash = LayoutHash.Compute(result.Sheets);
        return result;
    }

    /// <summary>Применяет переопределения отступа/зазора/мягкого отступа из материала.</summary>
    private static NestingOptions EffectiveOptions(NestingOptions options, SheetMaterial material)
    {
        if (material.SheetMarginMm is null && material.PartGapMm is null && material.AllowMarginRelax is null)
            return options;

        return new NestingOptions
        {
            PartGapMm = material.PartGapMm is double g && double.IsFinite(g) ? Math.Max(0, g) : options.PartGapMm,
            SheetMarginMm = material.SheetMarginMm is double m && double.IsFinite(m) ? Math.Max(0, m) : options.SheetMarginMm,
            Resolution = options.Resolution,
            MaxSearchPasses = options.MaxSearchPasses,
            NoImprovementStopCount = options.NoImprovementStopCount,
            AcceptableMaxUnplaced = options.AcceptableMaxUnplaced,
            AcceptableMaxSheets = options.AcceptableMaxSheets,
            PlacementHeuristic = options.PlacementHeuristic,
            OrientationPreference = options.OrientationPreference,
            PackStrategyIndex = options.PackStrategyIndex,
            EnableGapFilling = options.EnableGapFilling,
            AllowMarginRelax = material.AllowMarginRelax ?? options.AllowMarginRelax
        };
    }

    private static GroupCandidate PackGroup(
        List<(int CopyIndex, Part Part, int StableIndex)> copies,
        SheetMaterial material,
        NestingOptions options,
        PackingStrategy strategy,
        CancellationToken cancellationToken)
    {
        var sorted = PackingSorter.Sort(copies, strategy);
        var maxRects = PackWithMaxRectsPacker(sorted, material, options, cancellationToken);
        if (maxRects.Unplaced.Count == 0)
            return maxRects;

        var shelf = PackWithShelfPacker(sorted, material, options, cancellationToken);
        return IsBetter(shelf, maxRects, options) ? shelf : maxRects;
    }

    private static GroupCandidate PackWithMaxRectsPacker(
        IReadOnlyList<(int CopyIndex, Part Part, int StableIndex)> sorted,
        SheetMaterial material,
        NestingOptions options,
        CancellationToken cancellationToken)
    {
        var packers = new List<MaxRectsPacker>();
        var unplaced = new List<Part>();
        var unplacedCopyIndex = new Dictionary<Part, int>();

        foreach (var (copyIndex, part, _) in sorted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var placed = packers.Any(packer => packer.TryPlace(part, copyIndex));
            if (!placed && options.AllowMarginRelax)
                placed = TryPlaceRelaxed(packers, part, copyIndex, material, options);
            if (placed)
                continue;

            var newPacker = CreatePacker(material, options);
            if (newPacker.TryPlace(part, copyIndex))
            {
                packers.Add(newPacker);
            }
            else
            {
                var clone = CloneForUnplaced(part);
                unplaced.Add(clone);
                unplacedCopyIndex[clone] = copyIndex;
            }
        }

        GapFillingPacker.FillAndCompact(packers, unplaced, options, unplacedCopyIndex);

        if (options.AllowMarginRelax)
            TryPlaceRelaxedIntoExistingSheets(packers, unplaced, material, options, unplacedCopyIndex);

        var sheets = packers.Where(p => p.Parts.Count > 0)
            .Select((packer, index) => packer.ToSheet(index + 1))
            .ToList();
        var sheetArea = material.Length * material.Width;
        var utilization = sheets.Count > 0 && sheetArea > 0
            ? sheets.Sum(s => s.Parts.Sum(p => p.Source.TrueArea)) / (sheetArea * sheets.Count)
            : 0;
        return new GroupCandidate(sheets, unplaced, utilization);
    }

    /// <summary>
    /// Мягкий отступ: пытаемся влить деталь в свободное место уже заполненного
    /// листа — отступ игнорируется только для этой детали, зазор до соседей
    /// сохраняется. Возвращает true при успешном размещении.
    /// </summary>
    private static bool TryPlaceRelaxed(
        List<MaxRectsPacker> packers,
        Part part,
        int copyIndex,
        SheetMaterial material,
        NestingOptions options)
    {
        var gap = double.IsFinite(options.PartGapMm) ? Math.Max(0, options.PartGapMm) : 0;
        var resolution = double.IsFinite(options.Resolution) ? Math.Max(0, options.Resolution) : 0;

        var fitsPhysically = OrientationHelper
            .GetOrientations(part, material, resolution)
            .Any(o => NestingComparison.FitsWithin(o.Width, material.Length) &&
                      NestingComparison.FitsWithin(o.Height, material.Width));
        if (!fitsPhysically)
            return false;

        foreach (var packer in packers)
        {
            // Сиды: лист без отступа, засеянный уже размещёнными деталями —
            // свободные прямоугольники покрывают и кромочную зону.
            var seed = new MaxRectsPacker(material, 0, gap, resolution,
                options.PlacementHeuristic, options.OrientationPreference);
            var seeded = true;
            foreach (var existing in packer.Parts)
            {
                try
                {
                    seed.PlaceExisting(existing);
                }
                catch (InvalidOperationException)
                {
                    seeded = false;
                    break;
                }
            }
            if (!seeded)
                break;

            if (!seed.TryPlace(part, copyIndex))
                continue;

            var placed = seed.Parts[seed.Parts.Count - 1];
            packer.RegisterRelaxedPlacement(placed);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Мягкий отступ (пост-проход): для деталей, оставшихся неразмещёнными после
    /// gap-filling, пробуем влить их в свободное место существующих листов.
    /// </summary>
    private static void TryPlaceRelaxedIntoExistingSheets(
        List<MaxRectsPacker> packers,
        List<Part> unplaced,
        SheetMaterial material,
        NestingOptions options,
        IReadOnlyDictionary<Part, int> copyIndexOf)
    {
        if (unplaced.Count == 0 || packers.Count == 0)
            return;

        foreach (var part in unplaced.OrderByDescending(p => p.TrueArea).ToList())
        {
            var copyIndex = copyIndexOf.TryGetValue(part, out var ci) ? ci : 1;
            if (TryPlaceRelaxed(packers, part, copyIndex, material, options))
                unplaced.Remove(part);
        }
    }

    private static GroupCandidate PackWithShelfPacker(
        IReadOnlyList<(int CopyIndex, Part Part, int StableIndex)> sorted,
        SheetMaterial material,
        NestingOptions options,
        CancellationToken cancellationToken)
    {
        var packers = new List<ShelfPacker>();
        var unplaced = new List<Part>();

        foreach (var (copyIndex, part, _) in sorted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var placed = packers.Any(packer => packer.TryPlace(part, copyIndex));
            if (placed)
                continue;

            var newPacker = CreateShelfPacker(material, options);
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
            ? sheets.Sum(s => s.Parts.Sum(p => p.Source.TrueArea)) / (sheetArea * sheets.Count)
            : 0;
        return new GroupCandidate(sheets, unplaced, utilization);
    }

    private static bool IsBetter(GroupCandidate candidate, GroupCandidate current, NestingOptions options)
    {
        var candidateResult = ToNestingResult(candidate);
        var currentResult = ToNestingResult(current);
        return NestingResultComparison.IsBetter(candidateResult, currentResult, options);
    }

    private static NestingResult ToNestingResult(GroupCandidate candidate)
    {
        var result = new NestingResult();
        result.Sheets.AddRange(candidate.Sheets);
        result.Unplaced.AddRange(candidate.Unplaced);
        result.TotalUtilizationPercent = candidate.Utilization * 100.0;
        return result;
    }

    private static MaxRectsPacker CreatePacker(SheetMaterial material, NestingOptions options) =>
        new(material, options.SheetMarginMm, options.PartGapMm, options.Resolution,
            options.PlacementHeuristic, options.OrientationPreference);

    private static ShelfPacker CreateShelfPacker(SheetMaterial material, NestingOptions options) =>
        new(material, options.SheetMarginMm, options.PartGapMm, options.Resolution);

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
