using System.IO;
using System.Linq;
using NStCut.Core.Contracts;
using NStCut.Dxf.Services;
using Xunit;

namespace NStCut.Dxf.Tests;

public class CorpusTests
{
    [Fact]
    public void Corpus_ContainsFiles()
    {
        Assert.NotEmpty(Corpus.Enumerate());
    }

    [Fact]
    public void AllFiles_AreReadableAndHaveUsableBounds()
    {
        foreach (var path in Corpus.Enumerate())
        {
            var contour = new DxfGeometryService().ReadContour(path);

            Assert.True(contour.HasUsableBounds, $"{Path.GetFileName(path)} must have usable bounds.");
            Assert.NotEqual(DxfGeometryQuality.Invalid, contour.Quality);
        }
    }

    [Fact]
    public void AllFiles_AreDeterministicAcrossReloads()
    {
        foreach (var path in Corpus.Enumerate())
        {
            var first = new DxfGeometryService().ReadContour(path);
            var second = new DxfGeometryService().ReadContour(path);

            Assert.Equal(first.Quality, second.Quality);
            Assert.Equal(
                first.Diagnostics.Select(d => d.Code),
                second.Diagnostics.Select(d => d.Code));
        }
    }

    [Theory]
    [MemberData(nameof(GeometryCases))]
    public void File_MatchesExpectedGeometry(
        string fileName,
        DxfGeometryQuality expectedQuality,
        bool hasPolygon,
        int expectedHoles,
        double? expectedArea,
        double? tolerance)
    {
        var path = Corpus.Enumerate().Single(p => Path.GetFileName(p) == fileName);
        var contour = new DxfGeometryService().ReadContour(path);

        Assert.Equal(expectedQuality, contour.Quality);
        Assert.Equal(hasPolygon, contour.Polygon is not null);
        Assert.Equal(expectedHoles, contour.Holes.Count);

        if (contour.Polygon is not null)
        {
            Assert.True(contour.TrueArea > 0, $"{fileName} must have positive area.");
        }

        if (expectedArea.HasValue)
        {
            Assert.True(contour.Polygon is not null, "Polygon required for area check.");
            Assert.Equal(expectedArea.Value, contour.TrueArea, tolerance.GetValueOrDefault(0.5));
        }
    }

    public static TheoryData<string, DxfGeometryQuality, bool, int, double?, double?> GeometryCases { get; } = new()
    {
        { "corpus-arc-90.dxf", DxfGeometryQuality.BoundsOnly, false, 0, null, null },
        { "corpus-bulge-rect.dxf", DxfGeometryQuality.Exact, true, 0, null, null },
        { "corpus-C-outer.dxf", DxfGeometryQuality.Exact, true, 0, 52.0, 0.5 },
        { "corpus-circle-5.dxf", DxfGeometryQuality.Exact, true, 0, 78.0, 0.5 },
        { "corpus-L-outer.dxf", DxfGeometryQuality.Exact, true, 0, 75.0, 0.5 },
        { "corpus-rect-10x20.dxf", DxfGeometryQuality.Exact, true, 0, 200.0, 0.5 },
        { "corpus-rect-holes.dxf", DxfGeometryQuality.Exact, true, 2, 93.75, 0.5 },
        { "corpus-units-inch.dxf", DxfGeometryQuality.Exact, true, 0, 200.0, 0.5 },
        { "corpus-units-mm.dxf", DxfGeometryQuality.Exact, true, 0, 20000.0, 0.5 },
    };
}
