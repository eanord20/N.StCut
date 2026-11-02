using NStCut.Core.Contracts;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Dxf.Services;

namespace NStCut.Dxf.Tests;

public class DxfServicesTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public DxfServicesTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void WriteSheet_MissingDxfPath_Throws()
    {
        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 50 };
        var sheet = new SheetInstance { Material = material, Index = 1 };
        sheet.Parts.Add(new PlacedPart
        {
            Source = new Part { Number = "P1", DxfPath = string.Empty, Bounds = new Rect(0, 0, 10, 10) },
            CopyIndex = 1,
            Position = new Point(5, 5),
            Rotation = 0,
            Bounds = new Rect(5, 5, 10, 10)
        });

        var export = new DxfExportService();
        var outPath = Path.Combine(_tempDir, "sheet.dxf");

        Assert.Throws<ArgumentException>(() => export.WriteSheet(outPath, sheet, 5, 5));
    }

    [Fact]
    public void WriteSheet_NonExistentDxfPath_Throws()
    {
        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 50 };
        var sheet = new SheetInstance { Material = material, Index = 1 };
        sheet.Parts.Add(new PlacedPart
        {
            Source = new Part { Number = "P1", DxfPath = Path.Combine(_tempDir, "missing.dxf"), Bounds = new Rect(0, 0, 10, 10) },
            CopyIndex = 1,
            Position = new Point(5, 5),
            Rotation = 0,
            Bounds = new Rect(5, 5, 10, 10)
        });

        var export = new DxfExportService();
        var outPath = Path.Combine(_tempDir, "sheet.dxf");

        Assert.Throws<FileNotFoundException>(() => export.WriteSheet(outPath, sheet, 5, 5));
    }

    [Fact]
    public void ReadContour_ReturnsBoundsFromRectanglePolyline()
    {
        var path = Path.Combine(_tempDir, "rect.dxf");
        CreateRectangleDxf(path, 10, 20);

        var service = new DxfGeometryService();
        var contour = service.ReadContour(path);

        Assert.Equal(10, contour.Bounds.Width);
        Assert.Equal(20, contour.Bounds.Height);
    }

    [Fact]
    public void ReadContour_ClosedRectangleIsExact()
    {
        var path = Path.Combine(_tempDir, "exact.dxf");
        CreateRectangleDxf(path, 10, 20);

        var result = new DxfGeometryService().ReadContour(path);

        Assert.Equal(DxfGeometryQuality.Exact, result.Quality);
        Assert.True(result.HasUsableBounds);
        Assert.True(result.HasExactGeometry);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ReadContour_OpenPolylineIsBoundsOnly()
    {
        var path = Path.Combine(_tempDir, "open.dxf");
        var doc = new netDxf.DxfDocument();
        doc.Entities.Add(new netDxf.Entities.Polyline2D(new[] { new netDxf.Vector2(0, 0), new netDxf.Vector2(10, 20) }, false));
        doc.Save(path);

        var result = new DxfGeometryService().ReadContour(path);

        Assert.Equal(DxfGeometryQuality.BoundsOnly, result.Quality);
        Assert.Contains(result.Diagnostics, d => d.Code == DxfGeometryDiagnosticCode.NoClosedContour);
    }

    [Fact]
    public void ReadContour_EmptyDocumentIsInvalid()
    {
        var path = Path.Combine(_tempDir, "empty.dxf");
        new netDxf.DxfDocument().Save(path);

        var result = new DxfGeometryService().ReadContour(path);

        Assert.Equal(DxfGeometryQuality.Invalid, result.Quality);
        Assert.Contains(result.Diagnostics, d => d.Code == DxfGeometryDiagnosticCode.NoUsableBounds);
    }

    [Fact]
    public void ReadContour_ClosedLoopWithUnsupportedGeometryIsPartial()
    {
        var path = Path.Combine(_tempDir, "partial.dxf");
        var doc = new netDxf.DxfDocument();
        doc.Entities.Add(new netDxf.Entities.Polyline2D(new[]
        {
            new netDxf.Vector2(0, 0),
            new netDxf.Vector2(10, 0),
            new netDxf.Vector2(10, 20),
            new netDxf.Vector2(0, 20)
        }, true));
        doc.Entities.Add(new netDxf.Entities.Point(new netDxf.Vector2(1, 1)));
        doc.Save(path);

        var result = new DxfGeometryService().ReadContour(path);

        Assert.Equal(DxfGeometryQuality.Partial, result.Quality);
        Assert.True(result.HasUsableBounds);
        Assert.NotNull(result.Polygon);
        Assert.Contains(result.Diagnostics, d => d.Code == DxfGeometryDiagnosticCode.UnsupportedEntity);
    }

    [Fact]
    public void ReadContour_RepeatedReadProducesIdenticalClassificationAndDiagnostics()
    {
        var path = Path.Combine(_tempDir, "repeat.dxf");
        var doc = new netDxf.DxfDocument();
        doc.Entities.Add(new netDxf.Entities.Polyline2D(new[]
        {
            new netDxf.Vector2(0, 0),
            new netDxf.Vector2(10, 0),
            new netDxf.Vector2(10, 20),
            new netDxf.Vector2(0, 20)
        }, true));
        doc.Entities.Add(new netDxf.Entities.Point(new netDxf.Vector2(1, 1)));
        doc.Save(path);

        var first = new DxfGeometryService().ReadContour(path);
        var second = new DxfGeometryService().ReadContour(path);

        Assert.Equal(DxfGeometryQuality.Partial, first.Quality);
        Assert.Equal(first.Quality, second.Quality);
        Assert.Equal(
            first.Diagnostics.Select(d => d.Code),
            second.Diagnostics.Select(d => d.Code));
    }

    [Fact]
    public void ReadContour_CircleOnlyIsExactWithDeterministicGeometry()
    {
        var path = Path.Combine(_tempDir, "circle.dxf");
        var doc = new netDxf.DxfDocument();
        doc.Entities.Add(new netDxf.Entities.Circle(new netDxf.Vector2(5, 7), 3));
        doc.Save(path);

        var first = new DxfGeometryService().ReadContour(path);
        var second = new DxfGeometryService().ReadContour(path);

        Assert.Equal(DxfGeometryQuality.Exact, first.Quality);
        Assert.Empty(first.Diagnostics);
        Assert.Equal(first.Bounds, second.Bounds);
        Assert.Equal(first.Polygon!.Vertices, second.Polygon!.Vertices);
        Assert.Equal(first.Quality, second.Quality);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }

    [Fact]
    public void ReadContour_LineOnlyIsBoundsOnlyWithNoClosedContour()
    {
        var path = Path.Combine(_tempDir, "line.dxf");
        var doc = new netDxf.DxfDocument();
        doc.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(0, 0), new netDxf.Vector2(10, 20)));
        doc.Save(path);

        var result = new DxfGeometryService().ReadContour(path);

        Assert.Equal(DxfGeometryQuality.BoundsOnly, result.Quality);
        Assert.Equal(new[] { DxfGeometryDiagnosticCode.NoClosedContour }, result.Diagnostics.Select(d => d.Code));
    }

    [Fact]
    public void ReadContour_ArcOnlyIsBoundsOnlyWithNoClosedContour()
    {
        var path = Path.Combine(_tempDir, "arc.dxf");
        var doc = new netDxf.DxfDocument();
        doc.Entities.Add(new netDxf.Entities.Arc(new netDxf.Vector2(0, 0), 5, 0, 90));
        doc.Save(path);

        var result = new DxfGeometryService().ReadContour(path);

        Assert.Equal(DxfGeometryQuality.BoundsOnly, result.Quality);
        Assert.Equal(new[] { DxfGeometryDiagnosticCode.NoClosedContour }, result.Diagnostics.Select(d => d.Code));
    }

    [Fact]
    public void ReadContour_UnsupportedOnlyIsInvalidWithUnsupportedEntity()
    {
        var path = Path.Combine(_tempDir, "unsupported.dxf");
        var doc = new netDxf.DxfDocument();
        doc.Entities.Add(new netDxf.Entities.Point(new netDxf.Vector2(1, 1)));
        doc.Save(path);

        var result = new DxfGeometryService().ReadContour(path);

        Assert.Equal(DxfGeometryQuality.Invalid, result.Quality);
        Assert.Equal(
            new[] { DxfGeometryDiagnosticCode.UnsupportedEntity, DxfGeometryDiagnosticCode.NoUsableBounds },
            result.Diagnostics.Select(d => d.Code));
    }

    [Fact]
    public void ReadContour_MissingFileThrows()
    {
        var path = Path.Combine(_tempDir, "missing-contour.dxf");

        Assert.Throws<FileNotFoundException>(() => new DxfGeometryService().ReadContour(path));
    }

    [Fact]
    public void ReadClosed_ExceptionReturnsFalseWithDeterministicDiagnostic()
    {
        var first = new List<DxfGeometryDiagnostic>();
        var second = new List<DxfGeometryDiagnostic>();

        var firstResult = DxfGeometryService.ReadClosed(() => throw new InvalidOperationException(), first);
        var secondResult = DxfGeometryService.ReadClosed(() => throw new InvalidOperationException(), second);

        Assert.False(firstResult);
        Assert.False(secondResult);
        Assert.Equal(new[] { DxfGeometryDiagnosticCode.ClosedStateUnavailable }, first.Select(d => d.Code));
        Assert.Equal(first, second);
    }

    [Fact]
    public void ReadBulge_ExceptionReturnsZeroWithDeterministicDiagnostic()
    {
        var first = new List<DxfGeometryDiagnostic>();
        var second = new List<DxfGeometryDiagnostic>();

        var firstResult = DxfGeometryService.ReadBulge(() => throw new InvalidOperationException(), first);
        var secondResult = DxfGeometryService.ReadBulge(() => throw new InvalidOperationException(), second);

        Assert.Equal(0, firstResult);
        Assert.Equal(0, secondResult);
        Assert.Equal(new[] { DxfGeometryDiagnosticCode.BulgeUnavailable }, first.Select(d => d.Code));
        Assert.Equal(first, second);
    }

    [Fact]
    public void WriteSheet_CreatesOutputFile()
    {
        var sourcePath = Path.Combine(_tempDir, "part.dxf");
        CreateRectangleDxf(sourcePath, 10, 10);

        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 50 };
        var sheet = new SheetInstance { Material = material, Index = 1 };
        sheet.Parts.Add(new PlacedPart
        {
            Source = new Part { Number = "P1", DxfPath = sourcePath, Bounds = new Rect(0, 0, 10, 10) },
            CopyIndex = 1,
            Position = new Point(5, 5),
            Rotation = 0,
            Bounds = new Rect(5, 5, 10, 10)
        });

        var export = new DxfExportService();
        var outPath = Path.Combine(_tempDir, "sheet.dxf");
        export.WriteSheet(outPath, sheet, 5, 5);

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    [Fact]
    public void WriteSheet_HorizontalGrain_LabelAndArrowOutsideBoundary()
    {
        var material = new SheetMaterial
        {
            Name = "Steel",
            Length = 200,
            Width = 100,
            HasGrain = true,
            GrainDirection = GrainDirection.Horizontal
        };
        var sheet = new SheetInstance { Material = material, Index = 1 };
        var outPath = Path.Combine(_tempDir, "sheet-h.dxf");
        var labels = new DxfExportLabels("Sheet", "No grain", "Horizontal", "Vertical", "Material");

        new DxfExportService().WriteSheet(outPath, sheet, 5, 5, labels);

        var doc = netDxf.DxfDocument.Load(outPath);
        var b = GetBoundaryRect(doc);
        var text = Assert.Single(doc.Entities.Texts.Where(e => e.Layer.Name == "SheetLabels"));
        Assert.True(text.Position.Y > b.MaxY + Tolerance, "Label must be above boundary");
        Assert.False(IsInside(text.Position.X, text.Position.Y, b), "Label insertion must not be inside boundary");

        var arrowLines = doc.Entities.Lines.Where(e => e.Layer.Name == "GrainArrow").ToList();
        Assert.Equal(3, arrowLines.Count);
        foreach (var line in arrowLines)
        {
            Assert.True(line.StartPoint.Y > b.MaxY + Tolerance, "Arrow start must be above boundary");
            Assert.True(line.EndPoint.Y > b.MaxY + Tolerance, "Arrow end must be above boundary");
            Assert.False(IsInside(line.StartPoint.X, line.StartPoint.Y, b), "Arrow start must not be inside boundary");
            Assert.False(IsInside(line.EndPoint.X, line.EndPoint.Y, b), "Arrow end must not be inside boundary");
        }
    }

    [Fact]
    public void WriteSheet_VerticalGrain_LabelAndArrowOutsideBoundary()
    {
        var material = new SheetMaterial
        {
            Name = "Alum",
            Length = 100,
            Width = 200,
            HasGrain = true,
            GrainDirection = GrainDirection.Vertical
        };
        var sheet = new SheetInstance { Material = material, Index = 1 };
        var outPath = Path.Combine(_tempDir, "sheet-v.dxf");
        var labels = new DxfExportLabels("Sheet", "No grain", "Horizontal", "Vertical", "Material");

        new DxfExportService().WriteSheet(outPath, sheet, 5, 5, labels);

        var doc = netDxf.DxfDocument.Load(outPath);
        var b = GetBoundaryRect(doc);
        var text = Assert.Single(doc.Entities.Texts.Where(e => e.Layer.Name == "SheetLabels"));
        Assert.True(text.Position.Y > b.MaxY + Tolerance, "Label must be above boundary");
        Assert.False(IsInside(text.Position.X, text.Position.Y, b), "Label insertion must not be inside boundary");

        var arrowLines = doc.Entities.Lines.Where(e => e.Layer.Name == "GrainArrow").ToList();
        Assert.Equal(3, arrowLines.Count);
        foreach (var line in arrowLines)
        {
            Assert.True(line.StartPoint.Y > b.MaxY + Tolerance, "Arrow start must be above boundary");
            Assert.True(line.EndPoint.Y > b.MaxY + Tolerance, "Arrow end must be above boundary");
            Assert.False(IsInside(line.StartPoint.X, line.StartPoint.Y, b), "Arrow start must not be inside boundary");
            Assert.False(IsInside(line.EndPoint.X, line.EndPoint.Y, b), "Arrow end must not be inside boundary");
        }
    }

    [Fact]
    public void WriteSheet_NoGrain_LabelOnlyAndNoArrow()
    {
        var material = new SheetMaterial
        {
            Name = "Wood",
            Length = 150,
            Width = 80,
            HasGrain = false
        };
        var sheet = new SheetInstance { Material = material, Index = 1 };
        var outPath = Path.Combine(_tempDir, "sheet-nograin.dxf");
        var labels = new DxfExportLabels("Sheet", "No grain", "Horizontal", "Vertical", "Material");

        new DxfExportService().WriteSheet(outPath, sheet, 5, 5, labels);

        var doc = netDxf.DxfDocument.Load(outPath);
        var b = GetBoundaryRect(doc);
        var text = Assert.Single(doc.Entities.Texts.Where(e => e.Layer.Name == "SheetLabels"));
        Assert.True(text.Position.Y > b.MaxY + Tolerance, "Label must be above boundary");
        Assert.False(IsInside(text.Position.X, text.Position.Y, b), "Label insertion must not be inside boundary");
        Assert.Empty(doc.Entities.Lines.Where(e => e.Layer.Name == "GrainArrow"));
    }

    [Theory]
    [InlineData(0, 24, 11)]
    [InlineData(90, 11, 24)]
    public void WriteSheet_ActualPartBoundsMatchPlacementAndStayInsideSheet(double rotation, double placedWidth, double placedHeight)
    {
        const double tolerance = 1e-6;
        var sourcePath = Path.Combine(_tempDir, $"offset-part-{rotation}.dxf");
        CreateOffsetGeometryDxf(sourcePath, -13, 7, 24, 11);

        var material = new SheetMaterial { Name = "Steel", Length = 70, Width = 50 };
        var expected = new Rect(19, 14, placedWidth, placedHeight);
        var sheet = new SheetInstance { Material = material, Index = 1 };
        sheet.Parts.Add(new PlacedPart
        {
            Source = new Part { Number = "P1", DxfPath = sourcePath, Bounds = new Rect(-13, 7, 24, 11) },
            CopyIndex = 1,
            Position = new Point(expected.Left, expected.Bottom),
            Rotation = rotation,
            Bounds = expected
        });

        var outPath = Path.Combine(_tempDir, $"sheet-{rotation}.dxf");
        new DxfExportService().WriteSheet(outPath, sheet, 5, 5);

        var actual = ReadLayerBounds(outPath, "Parts");
        Assert.Equal(expected.Left, actual.Left, tolerance);
        Assert.Equal(expected.Bottom, actual.Bottom, tolerance);
        Assert.Equal(expected.Right, actual.Right, tolerance);
        Assert.Equal(expected.Top, actual.Top, tolerance);
        Assert.True(actual.Left >= -tolerance);
        Assert.True(actual.Bottom >= -tolerance);
        Assert.True(actual.Right <= material.Length + tolerance);
        Assert.True(actual.Top <= material.Width + tolerance);
    }

    [Fact]
    public void WriteCombined_MissingSourceDxfPath_Throws()
    {
        var sourcePath = Path.Combine(_tempDir, "missing-part.dxf");
        var sheet = new SheetInstance { Material = new SheetMaterial { Name = "Steel", Length = 100, Width = 50 }, Index = 1 };
        sheet.Parts.Add(new PlacedPart
        {
            Source = new Part { Number = "P1", DxfPath = sourcePath, Bounds = new Rect(0, 0, 10, 10) },
            CopyIndex = 1,
            Position = new Point(5, 5),
            Rotation = 0,
            Bounds = new Rect(5, 5, 10, 10)
        });

        var outPath = Path.Combine(_tempDir, "combined-missing.dxf");
        var sheets = new[] { sheet };

        Assert.Throws<FileNotFoundException>(() =>
            new DxfExportService().WriteCombined(outPath, sheets, 5, 5, new DxfExportLabels("Sheet", "No grain", "Horizontal", "Vertical")));
    }

    [Fact]
    public void WriteCombined_SmallThenTallerRow_RepresentsEverySheetWithoutOverlapAndUsesAsciiLabels()
    {
        var sheets = new[]
        {
            new SheetInstance { Material = new SheetMaterial { Id = "small", Name = "Steel", Length = 100, Width = 50 }, Index = 1 },
            new SheetInstance { Material = new SheetMaterial { Id = "tall-id", Name = " ", Length = 120, Width = 300, HasGrain = true, GrainDirection = GrainDirection.Vertical }, Index = 2 }
        };
        var path = Path.Combine(_tempDir, "combined.dxf");

        new DxfExportService().WriteCombined(path, sheets, 5, 5,
            new DxfExportLabels("Sheet", "No grain", "Horizontal", "Vertical", "Material"));

        var doc = netDxf.DxfDocument.Load(path);
        var boundaries = doc.Entities.Polylines2D.Where(x => x.Layer.Name == "SheetBoundary").ToList();
        Assert.Equal(sheets.Length, boundaries.Count);
        var bounds = boundaries.Select(boundary => new
        {
            MinX = boundary.Vertexes.Min(v => v.Position.X),
            MaxX = boundary.Vertexes.Max(v => v.Position.X),
            MinY = boundary.Vertexes.Min(v => v.Position.Y),
            MaxY = boundary.Vertexes.Max(v => v.Position.Y)
        }).ToList();
        for (var i = 0; i < bounds.Count; i++)
        for (var j = i + 1; j < bounds.Count; j++)
            Assert.True(bounds[i].MaxX <= bounds[j].MinX || bounds[j].MaxX <= bounds[i].MinX ||
                        bounds[i].MaxY <= bounds[j].MinY || bounds[j].MaxY <= bounds[i].MinY);
        Assert.True(bounds[1].MaxY < bounds[0].MinY);

        var labels = doc.Entities.Texts.Where(x => x.Layer.Name == "SheetLabels").Select(x => x.Value).ToArray();
        Assert.Equal(sheets.Length, labels.Length);
        Assert.Equal(new[]
        {
            "Material: Steel | No grain | Sheet 1",
            "Material: tall-id | Vertical | Sheet 2"
        }, labels);
        Assert.All(labels, label => Assert.DoesNotContain('—', label));
        Assert.Equal(3, doc.Entities.Lines.Count(x => x.Layer.Name == "GrainArrow"));
    }

    [Fact]
    public void WriteCombined_HorizontalVerticalAndNoGrain_LabelsAndArrowsOutsideAllBoundariesAndRowsNonOverlap()
    {
        var sheets = new[]
        {
            new SheetInstance
            {
                Material = new SheetMaterial
                {
                    Id = "a",
                    Name = "A",
                    Length = 100,
                    Width = 50,
                    HasGrain = true,
                    GrainDirection = GrainDirection.Horizontal
                },
                Index = 1
            },
            new SheetInstance
            {
                Material = new SheetMaterial
                {
                    Id = "b",
                    Name = "B",
                    Length = 80,
                    Width = 100,
                    HasGrain = false
                },
                Index = 2
            },
            new SheetInstance
            {
                Material = new SheetMaterial
                {
                    Id = "c",
                    Name = "C",
                    Length = 120,
                    Width = 300,
                    HasGrain = true,
                    GrainDirection = GrainDirection.Vertical
                },
                Index = 3
            }
        };
        var outPath = Path.Combine(_tempDir, "combined-hvn.dxf");
        var labels = new DxfExportLabels("Sheet", "No grain", "Horizontal", "Vertical", "Material");

        new DxfExportService().WriteCombined(outPath, sheets, 5, 5, labels);

        var doc = netDxf.DxfDocument.Load(outPath);
        var boundaries = GetBoundaryRects(doc);
        Assert.Equal(sheets.Length, boundaries.Count);

        for (var i = 0; i < boundaries.Count; i++)
        for (var j = i + 1; j < boundaries.Count; j++)
        {
            Assert.True(
                boundaries[i].MaxX <= boundaries[j].MinX || boundaries[j].MaxX <= boundaries[i].MinX ||
                boundaries[i].MaxY <= boundaries[j].MinY || boundaries[j].MaxY <= boundaries[i].MinY,
                $"Boundaries {i} and {j} must not overlap");
        }

        var texts = doc.Entities.Texts.Where(e => e.Layer.Name == "SheetLabels").ToList();
        Assert.Equal(sheets.Length, texts.Count);
        foreach (var text in texts)
        {
            foreach (var b in boundaries)
            {
                Assert.False(IsInside(text.Position.X, text.Position.Y, b), "Text must not be inside any boundary");
            }
        }

        var arrowLines = doc.Entities.Lines.Where(e => e.Layer.Name == "GrainArrow").ToList();
        Assert.Equal(6, arrowLines.Count);
        foreach (var line in arrowLines)
        {
            foreach (var b in boundaries)
            {
                Assert.False(IsInside(line.StartPoint.X, line.StartPoint.Y, b), "Arrow start must not be inside any boundary");
                Assert.False(IsInside(line.EndPoint.X, line.EndPoint.Y, b), "Arrow end must not be inside any boundary");
            }
        }
    }

    private const double Tolerance = 1e-6;

    private record BoundaryRect(double MinX, double MaxX, double MinY, double MaxY);

    private static BoundaryRect GetBoundaryRect(netDxf.DxfDocument doc)
    {
        var boundary = doc.Entities.Polylines2D.Single(p => p.Layer.Name == "SheetBoundary");
        var xs = boundary.Vertexes.Select(v => v.Position.X).ToList();
        var ys = boundary.Vertexes.Select(v => v.Position.Y).ToList();
        return new BoundaryRect(xs.Min(), xs.Max(), ys.Min(), ys.Max());
    }

    private static IReadOnlyList<BoundaryRect> GetBoundaryRects(netDxf.DxfDocument doc)
    {
        return doc.Entities.Polylines2D
            .Where(p => p.Layer.Name == "SheetBoundary")
            .Select(p =>
            {
                var xs = p.Vertexes.Select(v => v.Position.X).ToList();
                var ys = p.Vertexes.Select(v => v.Position.Y).ToList();
                return new BoundaryRect(xs.Min(), xs.Max(), ys.Min(), ys.Max());
            })
            .ToList();
    }

    private static bool IsInside(double x, double y, BoundaryRect b)
    {
        return x > b.MinX + Tolerance &&
               x < b.MaxX - Tolerance &&
               y > b.MinY + Tolerance &&
               y < b.MaxY - Tolerance;
    }

    private Rect ReadLayerBounds(string path, string layerName)
    {
        var output = netDxf.DxfDocument.Load(path);
        var layerOnly = new netDxf.DxfDocument();
        foreach (var entity in output.Entities.All.Where(entity => entity.Layer.Name == layerName))
            layerOnly.Entities.Add((netDxf.Entities.EntityObject)entity.Clone());

        var layerPath = Path.Combine(_tempDir, $"{layerName}-{Guid.NewGuid():N}.dxf");
        layerOnly.Save(layerPath);
        return new DxfGeometryService().ReadContour(layerPath).Bounds;
    }

    private static void CreateOffsetGeometryDxf(string path, double left, double bottom, double width, double height)
    {
        var doc = new netDxf.DxfDocument();
        doc.Entities.Add(new netDxf.Entities.Polyline2D(new[]
        {
            new netDxf.Vector2(left, bottom),
            new netDxf.Vector2(left + width, bottom),
            new netDxf.Vector2(left + width, bottom + height),
            new netDxf.Vector2(left, bottom + height)
        }, true));
        doc.Entities.Add(new netDxf.Entities.Line(
            new netDxf.Vector2(left + 2, bottom + 2),
            new netDxf.Vector2(left + width - 2, bottom + height - 2)));
        doc.Entities.Add(new netDxf.Entities.Circle(
            new netDxf.Vector2(left + width / 2, bottom + height / 2), 2));
        doc.Entities.Add(new netDxf.Entities.Arc(
            new netDxf.Vector2(left + width / 2, bottom + height / 2), 3, 15, 165));
        doc.Save(path);
    }

    private static void CreateRectangleDxf(string path, double width, double height)
    {
        var doc = new netDxf.DxfDocument();
        var rect = new netDxf.Entities.Polyline2D(new[]
        {
            new netDxf.Vector2(0, 0),
            new netDxf.Vector2(width, 0),
            new netDxf.Vector2(width, height),
            new netDxf.Vector2(0, height)
        }, true);
        doc.Entities.Add(rect);
        doc.Save(path);
    }
}
