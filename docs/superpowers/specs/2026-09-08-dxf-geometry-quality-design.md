# DXF Geometry Quality Design

**Date:** 2026-09-08  
**Roadmap:** NSC-004  
**Status:** Implemented and verified (311/311 tests passed; build completed with 0 warnings and 0 errors)

## Goal

Make every DXF geometry fallback explicit so the application never presents simplified or incomplete geometry as exact geometry.

## Scope

Included:

- quality classification on every successful `ReadContour` result;
- structured diagnostics for unsupported, skipped, or simplified geometry;
- explicit bbox fallback for usable but non-exact geometry;
- rejection of geometry without usable bounds;
- propagation to row state, nesting status, and UI warning;
- exact versus approximate area semantics;
- regression tests for service and application flow.

Excluded:

- polygon/NFP placement;
- support for new DXF entity types;
- changing arc/circle sampling precision;
- logging infrastructure unrelated to DXF diagnostics;
- strict mode that blocks all non-exact geometry.

## Contract

`IDxfGeometryService.ReadContour(string path)` continues to return `DxfContour`. The existing signature remains unchanged.

`DxfContour` gains:

```csharp
public DxfGeometryQuality Quality { get; set; }
public List<DxfGeometryDiagnostic> Diagnostics { get; set; } = new();
public bool HasUsableBounds { get; }
public bool HasExactGeometry { get; }
```

The new Core types contain no WPF dependencies:

```csharp
public enum DxfGeometryQuality
{
    Exact,
    Partial,
    BoundsOnly,
    Invalid
}

public sealed record DxfGeometryDiagnostic(DxfGeometryDiagnosticCode Code);

public enum DxfGeometryDiagnosticCode
{
    UnsupportedEntity,
    ClosedStateUnavailable,
    BulgeUnavailable,
    InvalidClosedLoop,
    NoClosedContour,
    NoUsableBounds
}
```

The Core diagnostic contract stores only the stable code; localized diagnostic text is produced in the App layer.

Diagnostic ordering is deterministic: diagnostics are appended in DXF entity enumeration order, with final aggregate diagnostics appended last.

## Quality Classification

### Exact

All conditions hold:

- usable finite positive bounds exist;
- a valid closed outer polygon exists;
- no entity affecting geometry was skipped or simplified;
- no parser diagnostic was produced.

### Partial

All conditions hold:

- usable finite positive bounds exist;
- a closed outer polygon exists;
- at least one entity or segment was skipped, unreadable, unsupported, or simplified.

The part may enter bbox nesting. Its area and perimeter are approximate for user-facing quality reporting.

### BoundsOnly

All conditions hold:

- usable finite positive bounds exist;
- no valid closed outer polygon exists;
- the service can still derive physical extents from supported entities.

The part may enter bbox nesting. Its area and perimeter use bbox fallback and are explicitly approximate.

### Invalid

Any of these conditions holds:

- no supported entity yields bounds;
- bounds contain NaN or Infinity;
- width or height is nonpositive;
- geometry cannot provide a safe bbox for nesting.

The part must not enter nesting.

## Exceptions

The service continues to throw for failures that prevent loading the DXF document, including a missing file, access failure, and malformed document load.

Quality statuses represent successfully loaded documents whose geometry quality can be assessed. They do not replace exceptional I/O and document-load failures.

## Entity Handling

### Closed `Polyline2D`

A valid loop with at least three usable points contributes to exact polygon geometry. Failure to read closed state adds `ClosedStateUnavailable`. A loop that cannot form a valid polygon adds `InvalidClosedLoop`.

### Open `Polyline2D`

Vertices contribute to bounds. If no valid closed outer polygon exists, the aggregate result is `BoundsOnly` and receives `NoClosedContour`.

### Bulge

A readable nonzero bulge is sampled using the existing deterministic sampling implementation. If bulge cannot be read, the segment may use the existing straight-line fallback, but `BulgeUnavailable` is recorded and quality cannot be `Exact`.

### Circle

A circle contributes a deterministic sampled closed loop and bounds using the existing sampling count.

### Line and Arc

Lines and arcs contribute to bounds. By themselves they do not establish a closed polygon. If they are the only usable geometry, quality is `BoundsOnly`.

### Unsupported entity

An unsupported geometric entity adds `UnsupportedEntity`. If exact outer geometry also exists, quality is `Partial`; if only usable supported bounds exist without an outer polygon, quality is `BoundsOnly`; if no usable bounds exist, quality is `Invalid`.

Non-geometric metadata entities that do not describe cut geometry must not produce warnings. The implementation must classify only entities from `doc.Entities.All` that are potential geometry.

## Area and Perimeter Semantics

For `Exact`, `DxfContour.TrueArea` uses outer polygon area minus holes, and exact-geometry metrics may be shown without warning.

For `Partial`, `BoundsOnly`, and `Invalid`, consumers must not label polygon-derived values as exact. Bbox nesting remains unchanged. For `Partial` and `BoundsOnly`, `Part.TrueArea` may use the existing bbox fallback, but UI/report quality indicators must state that metrics are approximate.

`Invalid` geometry must not contribute placed area, utilization, or report totals as a placed part.

## Application State

`PartStatus` remains responsible only for file resolution (`Pending`, `Found`, `ManuallySelected`, `NotFound`, `Ambiguous`). Geometry quality is not added to `PartStatus`.

`PartRow` gains:

```csharp
public DxfGeometryQuality GeometryQuality { get; set; } = DxfGeometryQuality.Invalid;
public string GeometryDiagnosticText { get; set; } = string.Empty;
public bool HasGeometryWarning =>
    GeometryQuality is DxfGeometryQuality.Partial or DxfGeometryQuality.BoundsOnly;
```

`GeometryDiagnosticText` is built by the App localization layer from diagnostic codes. Core and DXF infrastructure must not depend on localized UI strings.

## Data Flow

### File resolution and manual selection

`UpdateRowBounds` reads the contour and updates:

- `Bounds`;
- `GeometryQuality`;
- localized diagnostic text.

`Exact`, `Partial`, and `BoundsOnly` count as a readable DXF file. `Invalid` sets no usable bounds and is treated as geometry failure without changing the resolved path.

### Nesting

`PerformNesting` copies bounds, contour, holes, area, quality, and diagnostics from the read result.

- `Exact`: normal bbox nesting with exact quality metadata.
- `Partial`: bbox nesting plus warning.
- `BoundsOnly`: bbox nesting plus warning.
- `Invalid`: add every requested copy to invalid/unplaced results; do not call the nesting engine for that part.

The completion status includes a deterministic count of input rows using simplified geometry. It must not report `Partial` or `BoundsOnly` as a fully exact result.

### Preview and export

DXF export continues to clone and transform original entities; it does not export the approximated polygon. Geometry-quality warning therefore describes nesting and metrics, not destructive modification of the original DXF.

Preview and report data may show the quality status. No automatic change to source DXF is allowed.

## UI Presentation

The parts table displays a separate geometry-quality indicator:

- no warning indicator for `Exact`;
- warning indicator for `Partial` and `BoundsOnly`;
- error indicator for `Invalid`;
- tooltip containing localized diagnostic messages.

The indicator must work in light and dark themes and must not reuse file-resolution colors in a way that makes `Found` appear equivalent to exact geometry.

English and Ukrainian strings are required. Russian user-facing strings are not added.

## Determinism

Quality classification and diagnostic ordering must be deterministic for identical DXF input. Diagnostics must not contain timestamps, random IDs, exception stack traces, or environment-specific absolute paths.

Exception types may be mapped to stable diagnostic codes. Raw exception messages are not stored in Core results because they may vary by runtime and locale.

## Compatibility

- Existing implementations and fakes of `IDxfGeometryService` keep the same method signature.
- Existing object initializers for `DxfContour` compile because new properties have defaults.
- Existing exact DXF fixtures must classify as `Exact`.
- Existing line/open-polyline fixtures may change from an implicit fallback to explicit `BoundsOnly` without changing bounds.
- Project persistence does not serialize contour diagnostics; they are recomputed from source DXF when files are resolved or nesting runs.

## Acceptance Tests

1. Missing DXF still throws `FileNotFoundException`.
2. Existing valid closed-polyline fixture returns `Exact`, positive bounds, polygon, and no diagnostics.
3. Existing circle fixture returns `Exact` and deterministic sampled geometry.
4. Open polyline with positive extents returns `BoundsOnly` and `NoClosedContour`.
5. Line/arc-only geometry with positive extents returns `BoundsOnly` and `NoClosedContour`.
6. Empty loaded DXF returns `Invalid` and `NoUsableBounds`.
7. Unsupported geometry with no supported bounds returns `Invalid` and `UnsupportedEntity`.
8. Unsupported geometry plus a valid closed contour returns `Partial`.
9. Unreadable closed state cannot produce `Exact`.
10. Unreadable bulge records `BulgeUnavailable`; straight-line fallback cannot produce `Exact`.
11. Diagnostics retain deterministic entity order across repeated reads.
12. `UpdateRowBounds` treats `Exact`, `Partial`, and `BoundsOnly` as readable and exposes quality on `PartRow`.
13. `UpdateRowBounds` rejects `Invalid` without losing the selected path.
14. `PerformNesting` sends `Exact`, `Partial`, and `BoundsOnly` parts to the bbox engine.
15. `PerformNesting` excludes `Invalid` parts and preserves requested quantity in invalid/unplaced results.
16. Nesting completion reports the number of simplified-geometry rows.
17. Existing export tests prove original DXF entities remain the export source.
18. Existing DXF bounds and placement behavior remains unchanged for all supported fixtures.
19. Full solution build has zero warnings and zero errors.
20. Full solution test suite passes.

## Risks

- netDxf properties such as `IsClosed` and `Bulge` may be difficult to force to throw in a normal fixture. Their error paths are tested through a small internal adapter or pure classification helper rather than corrupting production DXF files.
- Treating every unsupported entity as cut geometry would create false warnings. Only potential geometric entities from the entity collection are classified.
- Existing reports may assume every positive bound has exact area. Quality metadata must reach report/UI before any metric is described as exact.
- Adding a new `PartStatus` value would conflate file resolution and geometry validity; this design explicitly avoids it.
