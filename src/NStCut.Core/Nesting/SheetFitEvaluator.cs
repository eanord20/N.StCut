using NStCut.Core.Models;

namespace NStCut.Core.Nesting;

/// <summary>
/// Pure evaluator that decides whether a single part's bounding box can fit onto the usable area
/// of an assigned sheet with the configured margin and part gap, respecting allowed orientations
/// and grain policy. It does not mutate the part or sheet.
/// </summary>
public static class SheetFitEvaluator
{
    public static SheetFitResult Evaluate(Part part, SheetMaterial material, NestingOptions options)
    {
        if (material is null || options is null)
            return SheetFitResult.NotEvaluated();

        if (part is null || part.Bounds.Width <= 0 || part.Bounds.Height <= 0)
            return SheetFitResult.NotEvaluated();

        // Отступ/зазор/мягкий отступ могут переопределяться в настройках материала.
        var marginValue = material.SheetMarginMm ?? options.SheetMarginMm;
        var margin = double.IsFinite(marginValue) ? Math.Max(0, marginValue) : 0;
        var usableWidth = material.Length - 2 * margin;
        var usableHeight = material.Width - 2 * margin;

        if (usableWidth <= 0 || usableHeight <= 0)
            return SheetFitResult.NotEvaluated();

        var orientations = OrientationPolicy.GetOrientations(part, material, options.Resolution).ToList();

        var fits = orientations.Any(o =>
            NestingComparison.FitsWithin(o.Width, usableWidth) &&
            NestingComparison.FitsWithin(o.Height, usableHeight));

        // Мягкий отступ: деталь считается помещающейся, если она входит хотя бы
        // в физический габарит листа без отступа.
        var allowRelax = material.AllowMarginRelax ?? options.AllowMarginRelax;
        if (!fits && allowRelax)
        {
            fits = orientations.Any(o =>
                NestingComparison.FitsWithin(o.Width, material.Length) &&
                NestingComparison.FitsWithin(o.Height, material.Width));
        }

        var grainBlocksRotation = material.HasGrain && part.GrainMode == GrainMode.FollowMaterial;

        return new SheetFitResult(
            true,
            fits,
            part.Bounds.Width,
            part.Bounds.Height,
            usableWidth,
            usableHeight,
            grainBlocksRotation,
            orientations);
    }
}
