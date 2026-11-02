using System.Windows;
using NStCut.App.Services;

namespace NStCut.App.Tests;

public class WindowPlacementHelperTests
{
    [Fact]
    public void ComputeWindowSize_ReturnsFractionOfWorkArea()
    {
        var workArea = new Rect(0, 0, 1000, 800);

        var size = WindowPlacementHelper.ComputeWindowSize(workArea, 0.9, 0.8);

        Assert.Equal(900, size.Width);
        Assert.Equal(640, size.Height);
    }

    [Theory]
    [InlineData(1200, 1000, 1.0, 1.0, 1200, 1000)]
    [InlineData(1200, 1000, 1.5, 1.5, 1200, 1000)]
    [InlineData(1200, 1000, 0.5, 0.5, 600, 500)]
    public void ComputeWindowSize_ClampsToWorkArea(double w, double h, double fracW, double fracH, double expectedW, double expectedH)
    {
        var workArea = new Rect(0, 0, w, h);

        var size = WindowPlacementHelper.ComputeWindowSize(workArea, fracW, fracH);

        Assert.Equal(expectedW, size.Width);
        Assert.Equal(expectedH, size.Height);
    }

    [Fact]
    public void ComputeWindowSize_RespectsMinimumSize()
    {
        var workArea = new Rect(0, 0, 400, 300);

        var size = WindowPlacementHelper.ComputeWindowSize(workArea, 0.1, 0.1, 300, 250);

        Assert.Equal(300, size.Width);
        Assert.Equal(250, size.Height);
    }

    [Fact]
    public void ComputeWindowSize_MinSizeStillClampedToWorkArea()
    {
        var workArea = new Rect(0, 0, 200, 150);

        var size = WindowPlacementHelper.ComputeWindowSize(workArea, 0.1, 0.1, 300, 250);

        Assert.Equal(200, size.Width);
        Assert.Equal(150, size.Height);
    }

    [Fact]
    public void CenterInWorkArea_ReturnsRectCenteredInWorkArea()
    {
        var workArea = new Rect(100, 50, 1000, 800);
        var size = new System.Windows.Size(400, 200);

        var rect = WindowPlacementHelper.CenterInWorkArea(workArea, size);

        Assert.Equal(400, rect.Left);
        Assert.Equal(350, rect.Top);
        Assert.Equal(400, rect.Width);
        Assert.Equal(200, rect.Height);
    }

    [Fact]
    public void ConvertPixelsToDips_UsesXAndYScales()
    {
        var pixels = new Rect(100, 200, 1920, 1080);

        var dips = WindowPlacementHelper.ConvertPixelsToDips(pixels, 1.5, 2.0);

        Assert.Equal(100 / 1.5, dips.Left, 3);
        Assert.Equal(200 / 2.0, dips.Top, 3);
        Assert.Equal(1920 / 1.5, dips.Width, 3);
        Assert.Equal(1080 / 2.0, dips.Height, 3);
    }

    [Fact]
    public void ConvertPixelsToDips_UsesUniformScale()
    {
        var pixels = new Rect(0, 0, 1920, 1080);

        var dips = WindowPlacementHelper.ConvertPixelsToDips(pixels, 1.25, 1.25);

        Assert.Equal(1536, dips.Width, 3);
        Assert.Equal(864, dips.Height, 3);
    }
}
