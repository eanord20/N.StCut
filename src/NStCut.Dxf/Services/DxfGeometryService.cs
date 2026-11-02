using System.Linq;
using netDxf;
using netDxf.Entities;
using NStCut.Core.Constants;
using NStCut.Core.Contracts;
using NStCut.Core.Geometry;
using Point = NStCut.Core.Geometry.Point;

namespace NStCut.Dxf.Services;

public sealed class DxfGeometryService : IDxfGeometryService
{
    public DxfContour ReadContour(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("DXF not found.", path);

        var doc = DxfDocument.Load(path);
        var bounds = new Rect(0, 0, 0, 0);
        var hasBounds = false;

        var closedLoops = new List<Polygon2D>();
        var diagnostics = new List<DxfGeometryDiagnostic>();
        var openSegments = new List<List<Point>>();

        foreach (var entity in doc.Entities.All)
        {
            // Служебные слои собственного DXF-экспорта — не геометрия детали.
            if (IsPresentationLayer(entity))
                continue;

            switch (entity)
            {
                case Polyline2D poly when TryGetClosed(poly, diagnostics):
                    var loopPoints = BuildPolylinePoints(poly, diagnostics);
                    if (loopPoints.Count >= 3)
                    {
                        var loop = new Polygon2D(loopPoints);
                        if (HasSelfIntersection(loop))
                            diagnostics.Add(new(DxfGeometryDiagnosticCode.SelfIntersectingContour));
                        closedLoops.Add(loop);
                        foreach (var p in loopPoints)
                            AddPoint(ref bounds, ref hasBounds, p.X, p.Y);
                    }
                    else
                    {
                        diagnostics.Add(new(DxfGeometryDiagnosticCode.InvalidClosedLoop));
                    }
                    break;

                case Circle circle:
                    var circlePoints = SampleCircle(circle);
                    if (circlePoints.Count >= 3)
                    {
                        closedLoops.Add(new Polygon2D(circlePoints));
                        foreach (var p in circlePoints)
                            AddPoint(ref bounds, ref hasBounds, p.X, p.Y);
                    }
                    break;

                case Line line:
                    AddPoint(ref bounds, ref hasBounds, line.StartPoint.X, line.StartPoint.Y);
                    AddPoint(ref bounds, ref hasBounds, line.EndPoint.X, line.EndPoint.Y);
                    openSegments.Add(new List<Point>
                    {
                        new(line.StartPoint.X, line.StartPoint.Y),
                        new(line.EndPoint.X, line.EndPoint.Y)
                    });
                    break;

                case Arc arc:
                    var arcPoints = SampleArc(arc);
                    AddRect(ref bounds, ref hasBounds, ArcBounds(arc));
                    if (arcPoints.Count >= 2)
                        openSegments.Add(arcPoints);
                    break;

                default:
                    if (entity is Polyline2D polyNotClosed)
                    {
                        var openPoints = BuildOpenPolylinePoints(polyNotClosed, diagnostics);
                        foreach (var p in openPoints)
                            AddPoint(ref bounds, ref hasBounds, p.X, p.Y);
                        if (openPoints.Count >= 2)
                            openSegments.Add(openPoints);
                    }
                    else if (!IsNonGeometricMetadata(entity))
                    {
                        diagnostics.Add(new(DxfGeometryDiagnosticCode.UnsupportedEntity));
                    }
                    break;
            }
        }

        // Contours drawn as separate LINE/ARC/open-polyline segments: chain them
        // into closed loops when endpoints join within tolerance.
        foreach (var loop in JoinSegmentsIntoClosedLoops(openSegments))
        {
            if (HasSelfIntersection(loop))
                diagnostics.Add(new(DxfGeometryDiagnosticCode.SelfIntersectingContour));
            closedLoops.Add(loop);
        }

        var contour = new DxfContour
        {
            Bounds = hasBounds ? bounds : new Rect(0, 0, 0, 0),
            Polygon = null,
            Holes = new List<Polygon2D>()
        };

        if (closedLoops.Count > 0)
        {
            var ordered = closedLoops
                .OrderByDescending(p => Math.Abs(p.SignedArea))
                .ToList();

            var outer = ordered[0];
            contour.Polygon = outer;

            var additionalOuters = 0;
            for (var i = 1; i < ordered.Count; i++)
            {
                var candidate = ordered[i];
                if (IsSameLoop(candidate, outer) || contour.Holes.Any(h => IsSameLoop(candidate, h)))
                {
                    diagnostics.Add(new(DxfGeometryDiagnosticCode.DuplicateContourIgnored));
                    continue;
                }

                if (IsHoleInside(candidate, outer) &&
                    !contour.Holes.Any(h => IsHoleInside(candidate, h)))
                {
                    contour.Holes.Add(candidate);
                }
                else
                {
                    additionalOuters++;
                }
            }

            if (additionalOuters > 0)
                diagnostics.Add(new(DxfGeometryDiagnosticCode.AdditionalContoursIgnored));
        }

        // When an exact outer contour exists, the part extent is defined by that
        // contour alone — stray open entities must not inflate the placement bounds.
        if (contour.Polygon is not null)
            contour.Bounds = contour.Polygon.Bounds;

        if (!contour.HasUsableBounds)
            diagnostics.Add(new(DxfGeometryDiagnosticCode.NoUsableBounds));
        else if (contour.Polygon is null)
            diagnostics.Add(new(DxfGeometryDiagnosticCode.NoClosedContour));

        contour.Diagnostics = diagnostics;
        contour.Quality = Classify(contour);

        return contour;
    }

    private static List<Polygon2D> JoinSegmentsIntoClosedLoops(List<List<Point>> segments)
    {
        var loops = new List<Polygon2D>();
        var tolerance = AppConstants.DxfSegmentJoinToleranceMm;
        var remaining = new List<List<Point>>(segments);

        while (remaining.Count > 0)
        {
            var chain = remaining[0];
            remaining.RemoveAt(0);
            var extended = true;

            while (extended && remaining.Count > 0)
            {
                extended = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var seg = remaining[i];
                    if (Near(chain[^1], seg[0], tolerance))
                    {
                        chain.AddRange(seg.Skip(1));
                    }
                    else if (Near(chain[^1], seg[^1], tolerance))
                    {
                        seg.Reverse();
                        chain.AddRange(seg.Skip(1));
                    }
                    else if (Near(chain[0], seg[^1], tolerance))
                    {
                        chain.InsertRange(0, seg.SkipLast(1));
                    }
                    else if (Near(chain[0], seg[0], tolerance))
                    {
                        seg.Reverse();
                        chain.InsertRange(0, seg.SkipLast(1));
                    }
                    else
                    {
                        continue;
                    }

                    remaining.RemoveAt(i);
                    extended = true;
                    break;
                }
            }

            if (chain.Count >= 4 && Near(chain[0], chain[^1], tolerance))
            {
                chain.RemoveAt(chain.Count - 1);
                if (chain.Count >= 3)
                    loops.Add(new Polygon2D(chain));
            }
        }

        return loops;
    }

    private static bool Near(Point a, Point b, double tolerance) =>
        Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance;

    private static List<Point> BuildOpenPolylinePoints(Polyline2D poly, List<DxfGeometryDiagnostic> diagnostics)
    {
        var vertices = poly.Vertexes;
        if (vertices.Count < 2)
            return new List<Point>();

        var result = new List<Point> { new(vertices[0].Position.X, vertices[0].Position.Y) };
        for (var i = 0; i < vertices.Count - 1; i++)
        {
            var bulge = GetBulge(vertices[i], diagnostics);
            AppendSegment(result, vertices[i].Position, vertices[i + 1].Position, bulge, skipEnd: false);
        }
        return result;
    }

    private static List<Point> SampleArc(Arc arc)
    {
        var samples = AppConstants.DxfArcSampleCount;
        if (samples < 2) samples = 2;

        var startDeg = arc.StartAngle;
        var endDeg = arc.EndAngle;
        if (endDeg <= startDeg)
            endDeg += 360.0;

        var points = new List<Point>(samples + 1);
        for (var i = 0; i <= samples; i++)
        {
            var angle = startDeg + (endDeg - startDeg) * i / samples;
            var rad = Math.PI * angle / 180.0;
            points.Add(new Point(
                arc.Center.X + arc.Radius * Math.Cos(rad),
                arc.Center.Y + arc.Radius * Math.Sin(rad)));
        }
        return points;
    }

    private static bool IsSameLoop(Polygon2D a, Polygon2D b)
    {
        if (a.Vertices.Count != b.Vertices.Count)
            return false;
        for (var i = 0; i < a.Vertices.Count; i++)
        {
            if (Math.Abs(a.Vertices[i].X - b.Vertices[i].X) > 1e-6 ||
                Math.Abs(a.Vertices[i].Y - b.Vertices[i].Y) > 1e-6)
                return false;
        }
        return true;
    }

    private static bool HasSelfIntersection(Polygon2D polygon)
    {
        var v = polygon.Vertices;
        var n = v.Count;
        for (var i = 0; i < n; i++)
        {
            var a1 = v[i];
            var a2 = v[(i + 1) % n];
            for (var j = i + 1; j < n; j++)
            {
                // Skip adjacent segments (sharing an endpoint).
                if (j == i || (j + 1) % n == i || (i + 1) % n == j)
                    continue;
                var b1 = v[j];
                var b2 = v[(j + 1) % n];
                if (SegmentsIntersect(a1, a2, b1, b2))
                    return true;
            }
        }
        return false;
    }

    private static bool SegmentsIntersect(Point a1, Point a2, Point b1, Point b2)
    {
        var d1 = Cross(b1, b2, a1);
        var d2 = Cross(b1, b2, a2);
        var d3 = Cross(a1, a2, b1);
        var d4 = Cross(a1, a2, b2);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static double Cross(Point o, Point a, Point b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    private static bool TryGetClosed(Polyline2D poly, List<DxfGeometryDiagnostic> diagnostics)
    {
        // netDxf exposes either a property or the closed flag through IsClosed.
        return ReadClosed(() => poly.IsClosed, diagnostics);
    }

    internal static bool ReadClosed(Func<bool> read, List<DxfGeometryDiagnostic> diagnostics)
    {
        try
        {
            return read();
        }
        catch
        {
            diagnostics.Add(new(DxfGeometryDiagnosticCode.ClosedStateUnavailable));
            return false;
        }
    }

    private static bool IsHoleInside(Polygon2D candidate, Polygon2D outer)
    {
        // All vertices of a hole must lie inside the outer contour,
        // and the candidate must be oriented opposite to the outer loop.
        var outerBounds = outer.Bounds;
        var candidateBounds = candidate.Bounds;
        if (!outerBounds.Contains(candidateBounds))
            return false;

        foreach (var p in candidate.Vertices)
        {
            if (!outer.ContainsPoint(p))
                return false;
        }

        return true;
    }

    private static List<Point> BuildPolylinePoints(Polyline2D poly, List<DxfGeometryDiagnostic> diagnostics)
    {
        var vertices = poly.Vertexes;
        if (vertices.Count < 2)
            return new List<Point>();

        var result = new List<Point> { new(vertices[0].Position.X, vertices[0].Position.Y) };

        for (var i = 0; i < vertices.Count; i++)
        {
            var nextIndex = (i + 1) % vertices.Count;
            if (nextIndex == 0 && i == vertices.Count - 1)
            {
                // Closing segment to the first vertex; don't duplicate the start point.
                var start = vertices[i].Position;
                var end = vertices[0].Position;
                var bulge = GetBulge(vertices[i], diagnostics);
                AppendSegment(result, start, end, bulge, skipEnd: true);
            }
            else
            {
                var start = vertices[i].Position;
                var end = vertices[nextIndex].Position;
                var bulge = GetBulge(vertices[i], diagnostics);
                AppendSegment(result, start, end, bulge, skipEnd: false);
            }
        }

        return result;
    }

    private static double GetBulge(Polyline2DVertex vertex, List<DxfGeometryDiagnostic> diagnostics)
    {
        return ReadBulge(() => vertex.Bulge, diagnostics);
    }

    internal static double ReadBulge(Func<double> read, List<DxfGeometryDiagnostic> diagnostics)
    {
        try
        {
            return read();
        }
        catch
        {
            diagnostics.Add(new(DxfGeometryDiagnosticCode.BulgeUnavailable));
            return 0.0;
        }
    }

    private static DxfGeometryQuality Classify(DxfContour contour)
    {
        if (!contour.HasUsableBounds)
            return DxfGeometryQuality.Invalid;
        if (contour.Polygon is null)
            return DxfGeometryQuality.BoundsOnly;
        return contour.Diagnostics.Count == 0
            ? DxfGeometryQuality.Exact
            : DxfGeometryQuality.Partial;
    }

    private static bool IsPresentationLayer(EntityObject entity)
    {
        var name = entity.Layer?.Name;
        return name is "SheetBoundary" or "SheetLabels" or "GrainArrow";
    }

    private static bool IsNonGeometricMetadata(EntityObject entity)
    {
        var type = entity.GetType();
        var name = type.Name;
        if (name is "Text" or "MText" or "Leader" or "MultiLeader" or "Attribute" or "AttributeDefinition" or "Tolerance")
            return true;
        if (type.IsSubclassOf(typeof(Dimension)))
            return true;
        return false;
    }

    private static void AppendSegment(List<Point> points, Vector2 start, Vector2 end, double bulge, bool skipEnd)
    {
        if (Math.Abs(bulge) < 1e-9)
        {
            if (!skipEnd)
                points.Add(new Point(end.X, end.Y));
            return;
        }

        var samples = AppConstants.DxfArcSampleCount;
        if (samples < 2) samples = 2;

        for (var i = 1; i < samples; i++)
        {
            var t = i / (double)samples;
            var p = InterpolateArc(start, end, bulge, t);
            points.Add(new Point(p.X, p.Y));
        }

        if (!skipEnd)
            points.Add(new Point(end.X, end.Y));
    }

    private static Vector2 InterpolateArc(Vector2 start, Vector2 end, double bulge, double t)
    {
        var chord = end - start;
        var chordLength = chord.Modulus();
        if (chordLength < 1e-9)
            return start;

        // Canonical bulge math: the included angle is 4*atan(bulge), the center
        // lies on the left normal of the directed chord for CCW (positive) bulges.
        // This avoids deriving the sweep from atan2 differences, which breaks on
        // ±PI wrap-around for long chords with tiny bulge values.
        var d = chordLength * 0.5;
        var h = d * (1.0 - bulge * bulge) / (2.0 * bulge);
        var radius = d * (1.0 + bulge * bulge) / (2.0 * Math.Abs(bulge));

        var perp = Normalize(new Vector2(-chord.Y, chord.X));
        var center = (start + end) * 0.5 + perp * h;

        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        var sweep = 4.0 * Math.Atan(bulge);

        var angle = startAngle + sweep * t;
        return new Vector2(
            center.X + radius * Math.Cos(angle),
            center.Y + radius * Math.Sin(angle));
    }

    private static Vector2 Normalize(Vector2 v)
    {
        var length = v.Modulus();
        return length < 1e-9 ? v : new Vector2(v.X / length, v.Y / length);
    }

    private static List<Point> SampleCircle(Circle circle)
    {
        var samples = AppConstants.DxfCircleSampleCount;
        if (samples < 3) samples = 3;

        var points = new List<Point>(samples);
        for (var i = 0; i < samples; i++)
        {
            var angle = 2.0 * Math.PI * i / samples;
            var x = circle.Center.X + circle.Radius * Math.Cos(angle);
            var y = circle.Center.Y + circle.Radius * Math.Sin(angle);
            points.Add(new Point(x, y));
        }

        return points;
    }

    private static Rect ArcBounds(Arc arc)
    {
        double startAngle = arc.StartAngle;
        double endAngle = arc.EndAngle;
        if (endAngle <= startAngle)
            endAngle += 360.0;

        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        void Include(double angleDeg)
        {
            var rad = Math.PI * angleDeg / 180.0;
            var x = arc.Center.X + arc.Radius * Math.Cos(rad);
            var y = arc.Center.Y + arc.Radius * Math.Sin(rad);
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        // Exact bounds: endpoints plus every cardinal angle the arc sweeps through.
        Include(startAngle);
        Include(endAngle);
        for (var quadrant = Math.Ceiling(startAngle / 90.0) * 90.0;
             quadrant <= endAngle;
             quadrant += 90.0)
        {
            Include(quadrant);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static void AddPoint(ref Rect bounds, ref bool hasBounds, double x, double y)
    {
        if (!hasBounds)
        {
            bounds = new Rect(x, y, 0, 0);
            hasBounds = true;
            return;
        }

        var minX = Math.Min(bounds.Left, x);
        var maxX = Math.Max(bounds.Right, x);
        var minY = Math.Min(bounds.Bottom, y);
        var maxY = Math.Max(bounds.Top, y);
        bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static void AddRect(ref Rect bounds, ref bool hasBounds, Rect rect)
    {
        AddPoint(ref bounds, ref hasBounds, rect.Left, rect.Bottom);
        AddPoint(ref bounds, ref hasBounds, rect.Right, rect.Top);
    }
}
