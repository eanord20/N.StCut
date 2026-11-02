using System.Linq;
using NStCut.Core.Geometry;
using NStCut.Nesting.Internals;
using Xunit;

namespace NStCut.Nesting.Tests;

public class NfpCalculatorTests
{
    private static Polygon2D Triangle(double x, double y, double a) =>
        new(new[]
        {
            new Point(x, y),
            new Point(x + a, y),
            new Point(x + a / 2, y + a)
        });

    private static Polygon2D Square(double x, double y, double s) =>
        new(new[]
        {
            new Point(x, y),
            new Point(x + s, y),
            new Point(x + s, y + s),
            new Point(x, y + s)
        });

    [Fact]
    public void ComputeNoFitPolygon_ConvexSquares_ReturnsConvexPolygon()
    {
        var a = Square(0, 0, 10);
        var b = Square(0, 0, 4);

        var nfp = NfpCalculator.ComputeNoFitPolygon(a, b);

        Assert.NotNull(nfp);
        Assert.True(nfp!.Vertices.Count >= 4);
    }

    [Fact]
    public void ComputeNoFitPolygon_OriginInsideNfp_ForOverlappingPlacement()
    {
        var a = Square(0, 0, 10);
        var b = Square(0, 0, 4);

        var nfp = NfpCalculator.ComputeNoFitPolygon(a, b)!;

        // If B is placed with its reference point at (0,0) and A occupies (0,0)-(10,10),
        // B at (0,0) overlaps A, so (0,0) must be inside the NFP.
        Assert.True(nfp.ContainsPoint(new Point(0, 0)));
    }

    [Fact]
    public void ComputeNoFitPolygon_FarAway_IsOutsideNfp()
    {
        var a = Square(0, 0, 10);
        var b = Square(0, 0, 4);

        var nfp = NfpCalculator.ComputeNoFitPolygon(a, b)!;

        // Placing B's reference point far from A should not overlap.
        Assert.False(nfp.ContainsPoint(new Point(100, 100)));
    }

    [Fact]
    public void ComputeNoFitPolygon_Concave_ReturnsNull()
    {
        // U-shape (concave).
        var concave = new Polygon2D(new[]
        {
            new Point(0, 0),
            new Point(10, 0),
            new Point(10, 2),
            new Point(6, 2),
            new Point(6, 6),
            new Point(10, 6),
            new Point(10, 8),
            new Point(0, 8)
        });
        var convex = Square(0, 0, 4);

        Assert.Null(NfpCalculator.ComputeNoFitPolygon(concave, convex));
        Assert.Null(NfpCalculator.ComputeNoFitPolygon(convex, concave));
    }

}
