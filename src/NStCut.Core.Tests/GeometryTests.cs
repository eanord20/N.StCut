using NStCut.Core.Geometry;

namespace NStCut.Core.Tests;

public class GeometryTests
{
    [Fact]
    public void Rect_Area_IsCorrect()
    {
        var rect = new Rect(0, 0, 10, 20);
        Assert.Equal(200, rect.Area);
    }

    [Fact]
    public void Rect_Intersects_ReturnsTrueForOverlapping()
    {
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);
        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Rect_Contains_ReturnsTrueWhenFullyInside()
    {
        var outer = new Rect(0, 0, 100, 100);
        var inner = new Rect(10, 10, 80, 80);
        Assert.True(outer.Contains(inner));
    }

    [Fact]
    public void Polygon_Bounds_ComputesMinMax()
    {
        var poly = new Polygon2D([new Point(1, 2), new Point(5, 8), new Point(3, 0)]);
        var bounds = poly.Bounds;
        Assert.Equal(1, bounds.Left);
        Assert.Equal(5, bounds.Right);
        Assert.Equal(0, bounds.Bottom);
        Assert.Equal(8, bounds.Top);
    }
}
