using System;
using System.Collections.Generic;
using System.Linq;
using NStCut.Core.Geometry;

namespace NStCut.Nesting.Internals;

/// <summary>
/// Computes the No-Fit Polygon (NFP) for convex polygons.
/// The NFP represents the locus of the reference point of the orbiting polygon B
/// such that B overlaps the stationary polygon A. Placements outside the NFP are feasible.
/// </summary>
internal static class NfpCalculator
{
    /// <summary>
    /// Computes the NFP for two counter-clockwise convex polygons.
    /// Returns <c>null</c> if either polygon is not convex or has fewer than 3 vertices.
    /// </summary>
    public static Polygon2D? ComputeNoFitPolygon(Polygon2D stationary, Polygon2D orbiting)
    {
        if (stationary.Vertices.Count < 3 || orbiting.Vertices.Count < 3)
            return null;
        if (!IsConvex(stationary) || !IsConvex(orbiting))
            return null;

        var minkowskiPoints = new List<Point>(stationary.Vertices.Count * orbiting.Vertices.Count);
        foreach (var a in stationary.Vertices)
        {
            foreach (var b in orbiting.Vertices)
            {
                minkowskiPoints.Add(new Point(a.X - b.X, a.Y - b.Y));
            }
        }

        var hull = ConvexHull(minkowskiPoints);
        if (hull.Count < 3)
            return null;

        return new Polygon2D(hull);
    }

    private static bool IsConvex(Polygon2D polygon)
    {
        var vertices = polygon.Vertices;
        var n = vertices.Count;
        if (n < 3) return false;

        var sign = 0;
        for (var i = 0; i < n; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % n];
            var c = vertices[(i + 2) % n];
            var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            var current = cross > 0 ? 1 : cross < 0 ? -1 : 0;
            if (current == 0) continue;
            if (sign == 0) sign = current;
            else if (sign != current) return false;
        }

        return true;
    }

    private static List<Point> ConvexHull(List<Point> points)
    {
        if (points.Count <= 1) return new List<Point>(points);

        var sorted = points
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .ToList();

        var lower = new List<Point>();
        foreach (var p in sorted)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
                lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }

        var upper = new List<Point>();
        for (var i = sorted.Count - 1; i >= 0; i--)
        {
            var p = sorted[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
                upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }

        // Remove duplicate end points.
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static double Cross(Point a, Point b, Point c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
}
