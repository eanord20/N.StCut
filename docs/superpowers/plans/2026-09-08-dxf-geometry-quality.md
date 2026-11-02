# DXF Geometry Quality Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Classify every successfully loaded DXF as Exact, Partial, BoundsOnly, or Invalid; propagate stable diagnostics through the application; warn on bbox fallback; and prevent invalid geometry from entering nesting.

**Architecture:** Extend the existing `DxfContour` contract without changing `IDxfGeometryService.ReadContour`. Keep deterministic classification and diagnostic codes in Core/DXF, then localize and display them only in App. Preserve current bbox nesting for Exact, Partial, and BoundsOnly; reject Invalid before calling the nesting engine.

**Tech Stack:** C# 12, .NET 8, WPF, xUnit, netDxf 3.0.1, existing MaterialDesignThemes controls.

## Global Constraints

- Geometry is the source of truth.
- Never modify original DXF entities.
- Business logic must never depend on WPF.
- Identical input always produces identical classification, diagnostics, and layouts.
- `PartStatus` remains file-resolution state and is not extended with geometry quality.
- Exact, Partial, and BoundsOnly may use bbox nesting; Invalid must not enter the engine.
- Diagnostic results contain stable codes, not paths, stack traces, timestamps, or localized runtime exception text.
- English and Ukrainian user-facing strings are required; Russian strings are not added.
- Existing `IDxfGeometryService.ReadContour(string)` signature remains unchanged.
- Do not add dependencies.
- Do not commit: the repository contains extensive unrelated staged and unstaged work.

## File Map

- Modify `src/NStCut.Core/Contracts/IDxfGeometryService.cs`: define quality and diagnostic contract on `DxfContour`.
- Modify `src/NStCut.Dxf/Services/DxfGeometryService.cs`: collect diagnostics and classify every loaded document.
- Modify `src/NStCut.Dxf.Tests/DxfServicesTests.cs`: exact, bounds-only, invalid, partial, and deterministic classification tests.
- Modify `src/NStCut.App/Models/PartRow.cs`: store geometry quality/codes and expose warning/error state.
- Modify `src/NStCut.App/ViewModels/MainViewModel.cs`: propagate quality during resolve and nesting; exclude Invalid; summarize fallback count.
- Modify `src/NStCut.App/Services/LocalizationService.cs`: map quality diagnostic codes to EN/UA text.
- Modify `src/NStCut.App/MainWindow.xaml`: show separate warning/error indicator in the status cell.
- Modify `src/NStCut.App.Tests/MainViewModelTests.cs`: resolve/nesting propagation and quantity tests.
- Modify `src/NStCut.App.Tests/ThemeResourceTests.cs` only if a new theme resource is needed; prefer existing MaterialDesign warning/error brushes.
- Modify `docs/superpowers/specs/2026-09-08-dxf-geometry-quality-design.md` and the unified plan only after full verification.

---

### Task 1: Add the Core Geometry-Quality Contract

**Files:**
- Modify: `src/NStCut.Core/Contracts/IDxfGeometryService.cs`
- Test: `src/NStCut.Core.Tests` is not required because the task defines data-only types; behavior is tested in Task 2 before parser changes.

**Interfaces:**
- Produces: `DxfGeometryQuality`, `DxfGeometryDiagnosticCode`, `DxfGeometryDiagnostic`, and defaulted `DxfContour` properties.
- Compatibility: existing `new DxfContour { Bounds = ... }` remains valid and defaults to `Invalid` until a service explicitly classifies it.

- [ ] Add the following types in `NStCut.Core.Contracts`:

```csharp
public enum DxfGeometryQuality
{
    Exact,
    Partial,
    BoundsOnly,
    Invalid
}

public enum DxfGeometryDiagnosticCode
{
    UnsupportedEntity,
    ClosedStateUnavailable,
    BulgeUnavailable,
    InvalidClosedLoop,
    NoClosedContour,
    NoUsableBounds
}

public sealed record DxfGeometryDiagnostic(DxfGeometryDiagnosticCode Code);
```

Do not store exception messages in the Core record.

- [ ] Extend `DxfContour`:

```csharp
public DxfGeometryQuality Quality { get; set; } = DxfGeometryQuality.Invalid;
public List<DxfGeometryDiagnostic> Diagnostics { get; set; } = new();
public bool HasUsableBounds =>
    double.IsFinite(Bounds.X) && double.IsFinite(Bounds.Y) &&
    double.IsFinite(Bounds.Width) && double.IsFinite(Bounds.Height) &&
    Bounds.Width > 0 && Bounds.Height > 0;
public bool HasExactGeometry => Quality == DxfGeometryQuality.Exact;
```

- [ ] Run:

```powershell
dotnet build "src\NStCut.Core\NStCut.Core.csproj" --nologo
```

Expected: 0 warnings, 0 errors.

---

### Task 2: Classify DXF Reads and Record Stable Diagnostics

**Files:**
- Modify: `src/NStCut.Dxf/Services/DxfGeometryService.cs`
- Test: `src/NStCut.Dxf.Tests/DxfServicesTests.cs`

**Interfaces:**
- Consumes: Task 1 Core types.
- Produces: every normal `ReadContour` return has an explicit quality and deterministic diagnostics.

- [ ] Add failing tests before changing the service:

```csharp
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
    var doc = new DxfDocument();
    doc.Entities.Add(new Polyline2D(new[] { new Vector2(0, 0), new Vector2(10, 20) }, false));
    doc.Save(path);

    var result = new DxfGeometryService().ReadContour(path);

    Assert.Equal(DxfGeometryQuality.BoundsOnly, result.Quality);
    Assert.Contains(result.Diagnostics, d => d.Code == DxfGeometryDiagnosticCode.NoClosedContour);
}

[Fact]
public void ReadContour_EmptyDocumentIsInvalid()
{
    var path = Path.Combine(_tempDir, "empty.dxf");
    new DxfDocument().Save(path);

    var result = new DxfGeometryService().ReadContour(path);

    Assert.Equal(DxfGeometryQuality.Invalid, result.Quality);
    Assert.Contains(result.Diagnostics, d => d.Code == DxfGeometryDiagnosticCode.NoUsableBounds);
}
```

- [ ] Run the three tests and verify RED because `Quality` remains its default `Invalid`.

```powershell
dotnet test "src\NStCut.Dxf.Tests\NStCut.Dxf.Tests.csproj" --nologo --filter "FullyQualifiedName~ClosedRectangleIsExact|FullyQualifiedName~OpenPolylineIsBoundsOnly|FullyQualifiedName~EmptyDocumentIsInvalid"
```

- [ ] Change private readers to report stable diagnostic codes:

```csharp
private static bool TryGetClosed(Polyline2D poly, List<DxfGeometryDiagnostic> diagnostics)
{
    try
    {
        return poly.IsClosed;
    }
    catch
    {
        diagnostics.Add(new(DxfGeometryDiagnosticCode.ClosedStateUnavailable));
        return false;
    }
}

private static double GetBulge(Polyline2DVertex vertex, List<DxfGeometryDiagnostic> diagnostics)
{
    try
    {
        return vertex.Bulge;
    }
    catch
    {
        diagnostics.Add(new(DxfGeometryDiagnosticCode.BulgeUnavailable));
        return 0;
    }
}
```

Pass the same diagnostics list through `BuildPolylinePoints` so diagnostic order follows entity/vertex order.

- [ ] After entity enumeration, classify with one pure helper:

```csharp
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
```

Before classification, append exactly one aggregate code:

```csharp
if (!contour.HasUsableBounds)
    diagnostics.Add(new(DxfGeometryDiagnosticCode.NoUsableBounds));
else if (contour.Polygon is null)
    diagnostics.Add(new(DxfGeometryDiagnosticCode.NoClosedContour));
```

Avoid duplicate aggregate codes.

- [ ] For a closed polyline with fewer than three generated points, add `InvalidClosedLoop`. For unsupported potential geometry in `doc.Entities.All`, add `UnsupportedEntity`; do not add warnings for non-geometric metadata.

- [ ] Add a pure classifier test matrix that proves:

```text
usable=false, polygon=false, diagnostics any -> Invalid
usable=true, polygon=false, diagnostics any -> BoundsOnly
usable=true, polygon=true, diagnostics empty -> Exact
usable=true, polygon=true, diagnostics nonempty -> Partial
```

Expose the helper as `internal` and use existing `InternalsVisibleTo` if available; otherwise test only through generated DXF fixtures.

- [ ] Add a repeated-read test asserting equal quality and diagnostic code sequence.

- [ ] Run all DXF tests:

```powershell
dotnet test "src\NStCut.Dxf.Tests\NStCut.Dxf.Tests.csproj" --nologo
```

Expected: all pass; existing bounds remain unchanged.

---

### Task 3: Propagate Geometry Quality to Part Rows

**Files:**
- Modify: `src/NStCut.App/Models/PartRow.cs`
- Modify: `src/NStCut.App/ViewModels/MainViewModel.cs`
- Modify: `src/NStCut.App/Services/LocalizationService.cs`
- Test: `src/NStCut.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: classified `DxfContour` from Task 2.
- Produces: `PartRow.GeometryQuality`, `GeometryDiagnostics`, `GeometryDiagnosticText`, `HasGeometryWarning`, and `HasGeometryError`.

- [ ] Add failing model-state tests:

```csharp
[Theory]
[InlineData(DxfGeometryQuality.Exact, false, false)]
[InlineData(DxfGeometryQuality.Partial, true, false)]
[InlineData(DxfGeometryQuality.BoundsOnly, true, false)]
[InlineData(DxfGeometryQuality.Invalid, false, true)]
public void GeometryQuality_ExposesWarningAndErrorState(
    DxfGeometryQuality quality, bool warning, bool error)
{
    var row = new PartRow { GeometryQuality = quality };
    Assert.Equal(warning, row.HasGeometryWarning);
    Assert.Equal(error, row.HasGeometryError);
}
```

- [ ] Extend `PartRow` with backing fields and notifications:

```csharp
public DxfGeometryQuality GeometryQuality { get; set; }
public IReadOnlyList<DxfGeometryDiagnosticCode> GeometryDiagnostics { get; set; }
public string GeometryDiagnosticText { get; set; }
public bool HasGeometryWarning => GeometryQuality is DxfGeometryQuality.Partial or DxfGeometryQuality.BoundsOnly;
public bool HasGeometryError => GeometryQuality == DxfGeometryQuality.Invalid;
```

Implement these using the existing `INotifyPropertyChanged` pattern. `GeometryQuality` must also notify `HasGeometryWarning` and `HasGeometryError`. Clone/FromPart do not persist diagnostics; default them to Invalid/empty until DXF is read again.

- [ ] Add EN/UA localization keys for each diagnostic code and these summaries:

```text
GeometryQualityPartial
GeometryQualityBoundsOnly
GeometryQualityInvalid
GeometrySimplifiedCountFormat
```

Use stable text such as:

```text
EN: "Closed contour was not found; bounding-box geometry is used."
UA: "Замкнений контур не знайдено; використовується геометрія габаритного прямокутника."
```

- [ ] Add `MainViewModel.FormatGeometryDiagnostics(DxfContour contour)` that maps codes through `LocalizationService`; if diagnostics are empty, use the quality summary key. Join messages with `Environment.NewLine` in code order.

- [ ] Add failing `UpdateRowBounds` tests using fake contours for Exact, BoundsOnly, and Invalid. Assert:

```text
Exact -> returns true, bounds copied, no warning
BoundsOnly -> returns true, bounds copied, warning true, text nonempty
Invalid -> returns false, Bounds default, error true, selected DxfPath unchanged
```

Use existing public assign/drop flows instead of reflection where possible.

- [ ] Update `UpdateRowBounds` to copy quality/codes/text and return `contour.HasUsableBounds && quality != Invalid`.

- [ ] Run App tests:

```powershell
dotnet test "src\NStCut.App.Tests\NStCut.App.Tests.csproj" --nologo --filter "FullyQualifiedName~GeometryQuality|FullyQualifiedName~BoundsOnly|FullyQualifiedName~InvalidGeometry"
```

Then run the complete App test project.

---

### Task 4: Enforce Geometry Quality in the Nesting Pipeline

**Files:**
- Modify: `src/NStCut.App/ViewModels/MainViewModel.cs`
- Test: `src/NStCut.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: Task 3 row state and Task 2 contour quality.
- Produces: Exact/Partial/BoundsOnly inputs reach bbox engine; Invalid inputs become invalid/unplaced copies; completion reports simplified-row count.

- [ ] Extend `RowNestingUpdate` without localized strings in worker data:

```csharp
internal sealed record RowNestingUpdate(
    int RowIndex,
    Rect Bounds,
    PartStatus Status,
    DxfGeometryQuality GeometryQuality,
    IReadOnlyList<DxfGeometryDiagnosticCode> GeometryDiagnostics);
```

- [ ] Add failing pipeline tests with a configurable fake geometry service and recording fake nesting engine:

```text
Partial with valid bounds -> engine receives part; update quality Partial
BoundsOnly with valid bounds -> engine receives part; update quality BoundsOnly
Invalid with Quantity=3 -> engine does not receive part; result contains three invalid/unplaced copies
Exact + Partial + BoundsOnly -> simplified row count is 2
```

Assert quantity using `result.Unplaced.Count`, not only row count.

- [ ] In `PerformNesting`, copy bounds/polygon/holes/area, then branch on quality:

```csharp
if (contour.Quality == DxfGeometryQuality.Invalid || !contour.HasUsableBounds)
{
    AddInvalidPart(input, invalidParts, updates, contour);
    continue;
}
```

Exact, Partial, and BoundsOnly continue to `SheetFitEvaluator` and `_nestingEngine.Nest`.

- [ ] Change `AddInvalidPart` and every `RowNestingUpdate` constructor to preserve quality and codes. I/O exceptions map to Invalid with an empty stable diagnostic list; UI uses the Invalid summary when no code exists.

- [ ] On the UI continuation after `PerformNesting`, apply each update to the corresponding row and build localized diagnostic text. Count distinct input rows whose quality is Partial or BoundsOnly.

- [ ] Append localized simplified count to the existing nesting completion status only when count is greater than zero:

```csharp
StatusText = $"{existingStatus} {string.Format(_localization["GeometrySimplifiedCountFormat"], simplifiedCount)}";
```

Keep existing status wording otherwise unchanged.

- [ ] Run focused and full App tests. Expected: invalid quantity preserved, fallback parts reach the engine, status count deterministic, existing nesting tests pass.

---

### Task 5: Add a Separate Geometry-Quality UI Indicator

**Files:**
- Modify: `src/NStCut.App/MainWindow.xaml`
- Modify: `src/NStCut.App/Services/LocalizationService.cs`
- Test: `src/NStCut.App.Tests/ThemeResourceTests.cs` or a new XAML resource assertion in the same test project.

**Interfaces:**
- Consumes: `PartRow.HasGeometryWarning`, `HasGeometryError`, `GeometryDiagnosticText`.
- Produces: warning/error icon in the existing status cell without changing file-resolution command behavior.

- [ ] Add a failing XAML/resource test that loads `MainWindow.xaml` as text or XML and verifies bindings for:

```text
HasGeometryWarning
HasGeometryError
GeometryDiagnosticText
```

Follow the existing resource/XAML test style; do not launch a real WPF window.

- [ ] Replace the status-cell single Button with a horizontal Grid or StackPanel containing:

```xml
<Button Content="{Binding Status}"
        Command="{Binding DataContext.ResolveStatusCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
        CommandParameter="{Binding}" />
<materialDesign:PackIcon Kind="AlertOutline"
                         ToolTip="{Binding GeometryDiagnosticText}"
                         Visibility="{Binding HasGeometryWarning, Converter={StaticResource BooleanToVisibilityConverter}}" />
<materialDesign:PackIcon Kind="AlertCircleOutline"
                         ToolTip="{Binding GeometryDiagnosticText}"
                         Visibility="{Binding HasGeometryError, Converter={StaticResource BooleanToVisibilityConverter}}" />
```

Use existing dynamic warning/error foreground resources if present. If none exist, add named light/dark theme resources and cover both dictionaries in `ThemeResourceTests`.

- [ ] Keep the status Button command and parameter unchanged. Localize its existing hardcoded tooltip using a new EN/UA `TooltipResolveDxf` key.

- [ ] Run App tests and build the App project. Manual smoke test remains required for icon contrast and tooltip wrapping.

---

### Task 6: Full Verification, Review, and Status Update

**Files:**
- Modify: `docs/superpowers/specs/2026-09-08-dxf-geometry-quality-design.md`
- Modify: `C:\Users\Nikolay\.devin\plans\plan-6530cd5c997264d8.md`

- [ ] Run all tests:

```powershell
dotnet test "src\NStCut.sln" --nologo
```

Record exact project and total counts.

- [ ] Run full build:

```powershell
dotnet build "src\NStCut.sln" --nologo
```

Expected: 0 warnings, 0 errors.

- [ ] Run scoped `git diff --check` over every file listed in the File Map. Expected: no whitespace errors.

- [ ] Dispatch an independent final reviewer. Required review checklist:

```text
No silent parser catch remains without a stable diagnostic.
Every successful ReadContour result is classified deterministically.
Exact requires polygon + usable bounds + no diagnostics.
BoundsOnly/Partial enter bbox nesting with warning.
Invalid never enters nesting and preserves quantity in unplaced/invalid results.
PartStatus remains unchanged.
Core/DXF have no WPF/localization dependency.
UI warning works independently of file-resolution status.
Original DXF export path remains unchanged.
```

- [ ] Resolve all Critical and Important findings through test-first fixes and rerun full verification.

- [ ] Change the design status to `Implemented and verified`. Mark NSC-004 `DONE` in the unified roadmap with exact tests/build evidence.

- [ ] Stop and report results. Do not begin NSC-005 automatically.
