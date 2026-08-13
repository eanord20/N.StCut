using NStCut.Core.Contracts;
using NStCut.Core.Models;
using NStCut.Nesting.Internals;

namespace NStCut.Nesting.Services;

public sealed class BoundingBoxShelfEngine : INestingEngine
{
    public NestingResult Nest(
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials,
        NestingOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new NestingResult();
        var materialDict = materials.ToDictionary(m => m.Id);

        var expanded = parts
            .SelectMany(p => Enumerable.Range(1, Math.Max(1, p.Quantity))
                .Select(i => (CopyIndex: i, Part: p)))
            .ToList();

        var groups = expanded.GroupBy(x => x.Part.MaterialId);
        int processed = 0;
        double totalParts = expanded.Count;

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!materialDict.TryGetValue(group.Key, out var material))
            {
                foreach (var item in group)
                    result.Unplaced.Add(CloneForUnplaced(item.Part));
                continue;
            }

            var copies = group.ToList();
            copies.Sort((a, b) => b.Part.Bounds.Area.CompareTo(a.Part.Bounds.Area));

            var packers = new List<ShelfPacker> { new(material, options.SheetMarginMm, options.PartGapMm) };

            foreach (var (copyIndex, part) in copies)
            {
                bool placed = false;
                foreach (var packer in packers)
                {
                    if (packer.TryPlace(part, copyIndex))
                    {
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    var newPacker = new ShelfPacker(material, options.SheetMarginMm, options.PartGapMm);
                    if (newPacker.TryPlace(part, copyIndex))
                    {
                        packers.Add(newPacker);
                    }
                    else
                    {
                        result.Unplaced.Add(CloneForUnplaced(part));
                    }
                }

                processed++;
                if (totalParts > 0)
                    progress?.Report(processed / totalParts * 100.0);
            }

            int sheetIndex = 1;
            foreach (var packer in packers)
            {
                if (packer.Parts.Count > 0)
                    result.Sheets.Add(packer.ToSheet(sheetIndex++));
            }
        }

        result.TotalUtilizationPercent = result.Sheets.Any()
            ? result.Sheets.Average(s => s.UtilizationPercent)
            : 0.0;

        return result;
    }

    private static Part CloneForUnplaced(Part part)
    {
        var clone = part.Clone();
        clone.Quantity = 1;
        return clone;
    }
}
