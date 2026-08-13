using NStCut.Core.Constants;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Nesting.Services;

namespace NStCut.Nesting.Tests;

public class BoundingBoxShelfEngineTests
{
    private static Part CreatePart(string name, double w, double h, string materialId, int qty = 1)
    {
        return new Part
        {
            Name = name,
            Quantity = qty,
            MaterialId = materialId,
            Bounds = new Rect(0, 0, w, h),
            Area = w * h,
            GrainMode = GrainMode.Allow90
        };
    }

    [Fact]
    public void Nest_FitsAllOnOneSheet()
    {
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 100 };
        var parts = new List<Part>
        {
            CreatePart("A", 40, 40, material.Id),
            CreatePart("B", 40, 40, material.Id)
        };

        var result = engine.Nest(parts, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);

        Assert.Single(result.Sheets);
        Assert.Equal(2, result.Sheets[0].Parts.Count);
        Assert.Empty(result.Unplaced);
    }

    [Fact]
    public void Nest_CreatesSecondSheetWhenNeeded()
    {
        // Default options add a 5mm sheet margin and 5mm part gap, so the usable
        // area is 50x50 (60 - 2*5). Two 45x45 parts each consume the full usable
        // width/height once gap is added, so the second copy must spill onto a
        // new sheet instance.
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Steel", Length = 60, Width = 60 };
        var parts = new List<Part>
        {
            CreatePart("A", 45, 45, material.Id),
            CreatePart("B", 45, 45, material.Id)
        };

        var result = engine.Nest(parts, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);

        Assert.Equal(2, result.Sheets.Count);
        Assert.Empty(result.Unplaced);
    }

    [Fact]
    public void Nest_MarksOversizedPartAsUnplaced()
    {
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Steel", Length = 10, Width = 10 };
        var parts = new List<Part>
        {
            CreatePart("A", 20, 20, material.Id)
        };

        var result = engine.Nest(parts, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);

        Assert.Empty(result.Sheets);
        Assert.Single(result.Unplaced);
    }

    [Fact]
    public void Nest_RotationReducesSheetCount()
    {
        // Usable area is 35x22 (margin 5 on a 45x32 sheet). Two 16x8 parts placed
        // unrotated fill the row width (2*(16+5)=42 > 35) and the wrapped row also
        // exceeds the usable height (2*(8+5)=26 > 22), so a second copy needs a new
        // sheet when rotation is disallowed. When 90-degree rotation is allowed,
        // the second copy rotates to 8x16 and fits beside the first copy on the
        // same shelf, so everything fits on a single sheet.
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Steel", Length = 45, Width = 32 };

        var noRotation = new List<Part>
        {
            CreatePart("A", 16, 8, material.Id, qty: 2)
        };
        noRotation[0].GrainMode = GrainMode.Fixed0;

        var withRotation = new List<Part>
        {
            CreatePart("A", 16, 8, material.Id, qty: 2)
        };
        withRotation[0].GrainMode = GrainMode.Allow90;

        var r1 = engine.Nest(noRotation, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);
        var r2 = engine.Nest(withRotation, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);

        Assert.Equal(2, r1.Sheets.Count);
        Assert.Single(r2.Sheets);
    }
}
