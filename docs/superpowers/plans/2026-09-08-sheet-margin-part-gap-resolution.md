# Sheet Margin, Part Gap, and Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make fit evaluation, MaxRects placement, persistence, and validation enforce one strict contract: margin applies only at sheet edges, gap applies only between parts, resolution never weakens physical constraints, and zero margin is fully supported.

**Architecture:** Keep the existing deterministic effective-rectangle MaxRects algorithm. Remove gap from single-part fit evaluation and add one virtual trailing gap to the packer's usable width/height so physical parts may touch all usable sheet boundaries while effective rectangles still preserve inter-part spacing. Preserve explicit zero settings by giving persisted settings real defaults and always loading nonnegative values.

**Tech Stack:** C# 12, .NET 8, WPF, xUnit, existing `NStCut.Core`, `NStCut.Nesting`, and `NStCut.App` projects.

## Global Constraints

- Geometry is the source of truth.
- Never modify original DXF entities.
- Identical input always produces identical layouts.
- Part quantity is never altered by the algorithm.
- Sheet boundaries are absolute constraints.
- `SheetMarginMm = 0`, `PartGapMm = 0`, and `Resolution = 0` are valid.
- `PartGapMm` applies only between different physical part bounds.
- `Resolution` must never permit boundary overflow or reduce `PartGapMm`.
- Business logic must never depend on WPF.
- Do not add dependencies.
- Do not commit during this plan: the target files already contain unrelated staged/uncommitted work. Review and commit isolation is a separate user-controlled step.

## File Map

- Modify `src/NStCut.App/Models/AppSettings.cs`: use canonical defaults while allowing explicit persisted zero values.
- Modify `src/NStCut.App/ViewModels/MainViewModel.cs`: load zero margin/gap instead of treating zero as absent.
- Modify `src/NStCut.App.Tests/AppSettingsServiceTests.cs`: verify explicit zero JSON round-trip.
- Modify `src/NStCut.App.Tests/MainViewModelTests.cs`: verify explicit zero is applied by the ViewModel.
- Modify `src/NStCut.Core/Nesting/SheetFitEvaluator.cs`: evaluate physical orientation against usable sheet dimensions without inter-part gap.
- Modify `src/NStCut.Core.Tests/SheetFitEvaluatorTests.cs`: define exact-fit, zero-margin, strict-overflow, and rotation behavior.
- Modify `src/NStCut.Nesting/Internals/MaxRectsPacker.cs`: use a virtual trailing gap while keeping physical bounds strict.
- Modify `src/NStCut.Nesting.Tests/MaxRectsPackerTests.cs`: verify physical bounds, gap, zero margin, resolution, repack, and deterministic behavior.
- Modify `src/NStCut.Nesting.Tests/BoundingBoxShelfEngineTests.cs` only if an existing assertion encodes the old edge-gap behavior.
- Modify `docs/superpowers/specs/2026-09-08-sheet-margin-part-gap-resolution-design.md`: change status to implemented only after all verification passes.
- Modify `C:\Users\Nikolay\.devin\plans\plan-6530cd5c997264d8.md`: mark NSC-003 done only after all verification passes.

---

### Task 1: Preserve Explicit Zero Settings

**Files:**
- Modify: `src/NStCut.App/Models/AppSettings.cs:4-13`
- Modify: `src/NStCut.App/ViewModels/MainViewModel.cs:168-189`
- Test: `src/NStCut.App.Tests/AppSettingsServiceTests.cs`
- Test: `src/NStCut.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `DefaultNestingOptions.Value.SheetMarginMm`, `DefaultNestingOptions.Value.PartGapMm`, `IAppSettingsService.Load()`.
- Produces: `AppSettings` whose missing JSON fields retain canonical defaults while explicit JSON zero remains zero; `MainViewModel.SheetMarginMm` and `PartGapMm` always reflect loaded nonnegative values.

- [ ] **Step 1: Add a failing persistence test for explicit zero**

Add to `AppSettingsServiceTests`:

```csharp
[Fact]
public void SaveThenLoad_PreservesExplicitZeroMarginAndGap()
{
    var service = CreateService();
    service.Save(new AppSettings { SheetMarginMm = 0, PartGapMm = 0 });

    var loaded = service.Load();

    Assert.Equal(0, loaded.SheetMarginMm);
    Assert.Equal(0, loaded.PartGapMm);
}
```

- [ ] **Step 2: Add a failing ViewModel load test**

Add to `MainViewModelTests`:

```csharp
[Fact]
public void Constructor_AppliesExplicitZeroMarginAndGapFromSettings()
{
    var settings = new FakeAppSettingsService(new AppSettings
    {
        SheetMarginMm = 0,
        PartGapMm = 0
    });

    var vm = new MainViewModel(
        new FakeNestingEngine(),
        new FakeDxfGeometryService(),
        new FakeDxfExportService(),
        new FakeReportService(),
        appSettingsService: settings);

    Assert.Equal(0, vm.SheetMarginMm);
    Assert.Equal(0, vm.PartGapMm);
}
```

Expected RED: `MainViewModel` retains `DefaultNestingOptions` because `LoadAppSettings()` currently applies values only when they are greater than zero.

- [ ] **Step 3: Run the focused RED tests**

Run:

```powershell
dotnet test "src\NStCut.App.Tests\NStCut.App.Tests.csproj" --nologo --filter "FullyQualifiedName~ExplicitZero"
```

Expected: the ViewModel test fails because zero is ignored. The service round-trip test may already pass and acts as a serialization guard.

- [ ] **Step 4: Give `AppSettings` canonical defaults**

In `AppSettings.cs`, import `NStCut.Core.Constants` and initialize the persisted geometry settings:

```csharp
public double SheetMarginMm { get; set; } = DefaultNestingOptions.Value.SheetMarginMm;
public double PartGapMm { get; set; } = DefaultNestingOptions.Value.PartGapMm;
```

This preserves defaults when a JSON property is absent while allowing deserialization of an explicit `0` to overwrite the initializer.

- [ ] **Step 5: Always load finite nonnegative margin and gap values**

Replace the `> 0` guards in `LoadAppSettings()` with:

```csharp
if (double.IsFinite(settings.SheetMarginMm) && settings.SheetMarginMm >= 0)
    SheetMarginMm = settings.SheetMarginMm;
if (double.IsFinite(settings.PartGapMm) && settings.PartGapMm >= 0)
    PartGapMm = settings.PartGapMm;
```

Invalid persisted values leave the canonical ViewModel defaults unchanged.

- [ ] **Step 6: Run App tests**

Run:

```powershell
dotnet test "src\NStCut.App.Tests\NStCut.App.Tests.csproj" --nologo
```

Expected: all App tests pass.

---

### Task 2: Make SheetFitEvaluator Use Physical Single-Part Fit

**Files:**
- Modify: `src/NStCut.Core/Nesting/SheetFitEvaluator.cs:12-42`
- Test: `src/NStCut.Core.Tests/SheetFitEvaluatorTests.cs`

**Interfaces:**
- Consumes: `Part.Bounds`, `SheetMaterial.Length/Width`, `NestingOptions.SheetMarginMm`, orientation policy.
- Produces: `SheetFitEvaluator.Evaluate(Part, SheetMaterial, NestingOptions)` where `PartGapMm` does not affect whether one part fits an empty sheet.

- [ ] **Step 1: Replace old gap-dependent test expectations with contract tests**

Add these tests and update old test comments/names that say gap reduces single-part usable area:

```csharp
[Theory]
[InlineData(0, 5)]
[InlineData(0, 100)]
[InlineData(1, 5)]
public void Evaluate_ExactUsableSizeFitsRegardlessOfPartGap(double margin, double gap)
{
    var material = Material(100, 50);
    var part = Part(100 - 2 * margin, 50 - 2 * margin, GrainMode.Fixed0);

    var result = SheetFitEvaluator.Evaluate(part, material, Options(margin, gap));

    Assert.True(result.WasEvaluated);
    Assert.True(result.Fits);
}

[Fact]
public void Evaluate_ResolutionNeverAllowsBoundaryOverflow()
{
    var options = Options(0, 10);
    options.Resolution = 0.5;

    var result = SheetFitEvaluator.Evaluate(
        Part(100.001, 50, GrainMode.Fixed0),
        Material(100, 50),
        options);

    Assert.False(result.Fits);
    Assert.True(result.IsOversized);
}
```

- [ ] **Step 2: Run focused Core tests and verify RED**

Run:

```powershell
dotnet test "src\NStCut.Core.Tests\NStCut.Core.Tests.csproj" --nologo --filter "FullyQualifiedName~SheetFitEvaluatorTests"
```

Expected: exact-fit cases with nonzero gap fail under the old `orientation + PartGapMm <= usable` rule.

- [ ] **Step 3: Implement strict physical fit**

In `SheetFitEvaluator.Evaluate`, normalize only valid inputs and compare physical orientation dimensions:

```csharp
var margin = double.IsFinite(options.SheetMarginMm) ? Math.Max(0, options.SheetMarginMm) : 0;
var usableWidth = material.Length - 2 * margin;
var usableHeight = material.Width - 2 * margin;
```

Replace the fit predicate with:

```csharp
var fits = orientations.Any(o =>
    NestingComparison.FitsWithin(o.Width, usableWidth) &&
    NestingComparison.FitsWithin(o.Height, usableHeight));
```

Do not add resolution to either physical limit.

- [ ] **Step 4: Run Core tests**

Run:

```powershell
dotnet test "src\NStCut.Core.Tests\NStCut.Core.Tests.csproj" --nologo
```

Expected: all Core tests pass, including strict overflow cases.

---

### Task 3: Correct MaxRects Edge-Gap Semantics

**Files:**
- Modify: `src/NStCut.Nesting/Internals/MaxRectsPacker.cs:34-49`
- Test: `src/NStCut.Nesting.Tests/MaxRectsPackerTests.cs`

**Interfaces:**
- Consumes: physical usable dimensions from material and margin; existing effective bounds `part + gap`.
- Produces: a virtual free rectangle of `(usableWidth + gap) × (usableHeight + gap)` while all `PlacedPart.Bounds` remain inside the physical usable rectangle.

- [ ] **Step 1: Add failing full-sheet and exact-margin tests**

Add to `MaxRectsPackerTests`:

```csharp
[Theory]
[InlineData(0, 1)]
[InlineData(0, 25)]
[InlineData(1, 1)]
public void TryPlace_ExactUsablePartFitsWithoutEdgeGap(double margin, double gap)
{
    var material = new SheetMaterial { Length = 100, Width = 50 };
    var packer = new MaxRectsPacker(material, margin, gap, 0.01);
    var part = Part("Exact", 100 - 2 * margin, 50 - 2 * margin, material);

    Assert.True(packer.TryPlace(part, 1));
    var placed = Assert.Single(packer.Parts);
    Assert.Equal(margin, placed.Bounds.Left);
    Assert.Equal(material.Length - margin, placed.Bounds.Right);
    Assert.Equal(margin, placed.Bounds.Bottom);
    Assert.Equal(material.Width - margin, placed.Bounds.Top);
}
```

- [ ] **Step 2: Add strict overflow and full inter-part gap tests**

```csharp
[Fact]
public void TryPlace_ResolutionNeverAllowsPhysicalOverflow()
{
    var material = new SheetMaterial { Length = 100, Width = 50 };
    var packer = new MaxRectsPacker(material, 0, 10, 0.5);

    Assert.False(packer.TryPlace(Part("TooWide", 100.001, 50, material), 1));
}

[Fact]
public void TryPlace_PreservesFullGapOnlyBetweenParts()
{
    var material = new SheetMaterial { Length = 100, Width = 50 };
    var packer = new MaxRectsPacker(material, 0, 10, 0.5);

    Assert.True(packer.TryPlace(Part("A", 45, 50, material), 1));
    Assert.True(packer.TryPlace(Part("B", 45, 50, material), 1));

    AssertLayoutIsPhysicallyValid(packer.Parts, material, 0, 10);
    Assert.Equal(0, packer.Parts.Min(p => p.Bounds.Left));
    Assert.Equal(100, packer.Parts.Max(p => p.Bounds.Right));
}
```

- [ ] **Step 3: Run focused Nesting tests and verify RED**

Run:

```powershell
dotnet test "src\NStCut.Nesting.Tests\NStCut.Nesting.Tests.csproj" --nologo --filter "FullyQualifiedName~TryPlace_ExactUsablePartFitsWithoutEdgeGap|FullyQualifiedName~TryPlace_PreservesFullGapOnlyBetweenParts"
```

Expected: old packer rejects exact usable parts when gap is nonzero.

- [ ] **Step 4: Implement virtual trailing gap**

In the constructor, sanitize values and initialize the virtual free rectangle:

```csharp
_margin = double.IsFinite(margin) ? Math.Max(0, margin) : 0;
_gap = double.IsFinite(gap) ? Math.Max(0, gap) : 0;
_resolution = double.IsFinite(resolution) ? Math.Max(0, resolution) : 0;

var usableWidth = material.Length - 2 * _margin;
var usableHeight = material.Width - 2 * _margin;
if (usableWidth > 0 && usableHeight > 0)
    _freeRectangles.Add(new Rect(0, 0, usableWidth + _gap, usableHeight + _gap));
```

Keep `effectiveWidth = width + _gap`, `effectiveHeight = height + _gap`, and physical `PlacedPart.Bounds` unchanged.

- [ ] **Step 5: Add a physical-bound guard before accepting a candidate**

Candidate selection must explicitly reject any physical bound outside the usable rectangle, independent of resolution:

```csharp
var physicalRight = free.X + width;
var physicalTop = free.Y + height;
if (!NestingComparison.FitsWithin(physicalRight, usableWidth) ||
    !NestingComparison.FitsWithin(physicalTop, usableHeight))
    continue;
```

Store `usableWidth` and `usableHeight` as private readonly fields initialized by the constructor so both `TryPlace` and `TryPlaceAt` apply the same guard.

- [ ] **Step 6: Apply the same guard to `TryPlaceAt` and `PlaceExisting`**

`TryPlaceAt` must reject target positions whose physical part exceeds `_usableWidth/_usableHeight`. `PlaceExisting` must validate existing physical bounds before recording them; use an `InvalidOperationException` for an internally inconsistent existing layout rather than silently constructing an invalid repack.

- [ ] **Step 7: Run all Nesting tests**

Run:

```powershell
dotnet test "src\NStCut.Nesting.Tests\NStCut.Nesting.Tests.csproj" --nologo
```

Expected: all Nesting tests pass; update only assertions that encoded the old extra right/top edge gap.

---

### Task 4: Verify Every Placement Path and Determinism

**Files:**
- Test: `src/NStCut.Nesting.Tests/MaxRectsPackerTests.cs`
- Test: `src/NStCut.Nesting.Tests/BoundingBoxShelfEngineTests.cs`
- Modify: `src/NStCut.Nesting/Internals/MaxRectsPacker.cs` only if a failing path exposes a shared-contract defect.

**Interfaces:**
- Consumes: corrected `MaxRectsPacker` from Task 3.
- Produces: regression evidence that normal placement, targeted gap filling, repacking, and engine-level repeated runs satisfy identical physical constraints.

- [ ] **Step 1: Add a reusable strict physical validator**

Update `AssertLayoutIsPhysicallyValid` so sheet-edge checks do not add gap:

```csharp
Assert.True(placed.Bounds.Left >= margin);
Assert.True(placed.Bounds.Bottom >= margin);
Assert.True(placed.Bounds.Right <= material.Length - margin);
Assert.True(placed.Bounds.Top <= material.Width - margin);
```

Keep pairwise checks with the full configured gap.

- [ ] **Step 2: Add targeted placement and repack coverage**

```csharp
[Fact]
public void TryPlaceAtAndRepack_PreserveZeroMarginAndPhysicalGapContract()
{
    var material = new SheetMaterial { Length = 100, Width = 50 };
    var packer = new MaxRectsPacker(material, 0, 10, 0.01);
    Assert.True(packer.TryPlace(Part("A", 45, 50, material), 1));
    Assert.True(packer.TryPlaceAt(new Rect(55, 0, 55, 60), Part("B", 45, 50, material), 1));

    var repacked = packer.Repack(packer.Parts);

    AssertLayoutIsPhysicallyValid(repacked.Parts, material, 0, 10);
}
```

The virtual target rectangle may extend into the trailing virtual strip, but the physical part must end at the sheet boundary.

- [ ] **Step 3: Add engine evaluator-consistency coverage**

For a table of margin/gap/resolution/exact/overflow inputs, assert that `SheetFitEvaluator.Evaluate(...).Fits` equals whether an empty `MaxRectsPacker.TryPlace(...)` succeeds. Include margin `0`, gap `0`, nonzero gap, resolution `0`, and overflow `0.001` with resolution `0.5`.

Use this concrete data:

```csharp
[Theory]
[InlineData(100, 50, 0, 10, 0.5, true)]
[InlineData(100.001, 50, 0, 10, 0.5, false)]
[InlineData(98, 48, 1, 10, 0, true)]
[InlineData(98.001, 48, 1, 10, 0.5, false)]
public void EvaluatorAndEmptyPacker_Agree(
    double width, double height, double margin, double gap, double resolution, bool expected)
```

- [ ] **Step 4: Run focused tests**

Run:

```powershell
dotnet test "src\NStCut.Nesting.Tests\NStCut.Nesting.Tests.csproj" --nologo --filter "FullyQualifiedName~TryPlaceAtAndRepack|FullyQualifiedName~EvaluatorAndEmptyPacker"
```

Expected: PASS. If a path fails, change the shared packer contract rather than adding a path-specific tolerance.

- [ ] **Step 5: Run deterministic engine tests twice**

Run twice:

```powershell
dotnet test "src\NStCut.Nesting.Tests\NStCut.Nesting.Tests.csproj" --nologo --filter "FullyQualifiedName~Determin"
```

Expected: both runs pass with identical asserted positions/hashes.

---

### Task 5: Full Verification and Documentation Status

**Files:**
- Modify: `docs/superpowers/specs/2026-09-08-sheet-margin-part-gap-resolution-design.md:1-6`
- Modify: `C:\Users\Nikolay\.devin\plans\plan-6530cd5c997264d8.md`

**Interfaces:**
- Consumes: Tasks 1–4 and their passing tests.
- Produces: verified NSC-003 completion record.

- [ ] **Step 1: Run the complete test suite**

```powershell
dotnet test "src\NStCut.sln" --nologo
```

Expected: 0 failed tests. Record the exact total; do not reuse an earlier count.

- [ ] **Step 2: Run the complete build**

```powershell
dotnet build "src\NStCut.sln" --nologo
```

Expected: 0 warnings and 0 errors.

- [ ] **Step 3: Inspect the scoped diff**

```powershell
git diff --check -- "src/NStCut.App/Models/AppSettings.cs" "src/NStCut.App/ViewModels/MainViewModel.cs" "src/NStCut.App.Tests/AppSettingsServiceTests.cs" "src/NStCut.App.Tests/MainViewModelTests.cs" "src/NStCut.Core/Nesting/SheetFitEvaluator.cs" "src/NStCut.Core.Tests/SheetFitEvaluatorTests.cs" "src/NStCut.Nesting/Internals/MaxRectsPacker.cs" "src/NStCut.Nesting.Tests/MaxRectsPackerTests.cs" "src/NStCut.Nesting.Tests/BoundingBoxShelfEngineTests.cs"
```

Expected: no whitespace errors.

- [ ] **Step 4: Request independent code review**

Reviewer checklist:

- no physical bound exceeds `SheetMarginMm`;
- no pairwise gap is smaller than `PartGapMm`;
- `Resolution` does not weaken either constraint;
- margin zero is restored from settings and works in evaluator/packer/repack;
- no deterministic ordering or quantity behavior changed;
- old tests were updated only where they encoded the superseded edge-gap contract.

Resolve all Critical and Important findings, then rerun Steps 1–3.

- [ ] **Step 5: Update status documents**

Change the design spec status to `Implemented and verified` and mark NSC-003 `DONE` in the unified plan. Include the exact final test count and build result.

- [ ] **Step 6: Stop for user review**

Do not start NSC-004 automatically. Present changed files, RED/GREEN evidence, complete verification, review findings, and any manual checks still required.
