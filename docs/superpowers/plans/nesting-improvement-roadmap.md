# Nesting Improvement Roadmap — N.StCut

**Date:** 2026-09-02
**Basis:** `docs/superpowers/specs/nesting-quality-specification.md`, `docs/superpowers/specs/nesting-objective-function.md`, `docs/superpowers/reports/nesting-current-algorithm-audit.md`

---

## Goal

Move N.StCut from a bounding-box MaxRects packer to a quality-aware nesting system that supports true geometry, better residuals, and production-ready layouts.

---

## Stage 1 — MVP (Completed / Immediate)

Status: **mostly implemented** in the current cycle.

- [x] Define quality criteria (Document A).
- [x] Define lexicographic objective function (Document B).
- [x] Define algorithm requirements and 10 reference scenarios (Document C).
- [x] Implement `NestingQualityMetrics` and `NestingQualityScore`.
- [x] Update `NestingResultComparison` to use the new score.
- [x] Update `BoundingBoxShelfEngine.GroupCandidate.IsBetter` to use the new score.
- [x] Store free rectangles in `SheetInstance` from `MaxRectsPacker`.
- [x] Extend `NestingPassResult` and `MultiPassNestingEngine` logging with new metrics.
- [x] Add `Polygon2D.Area` and `Perimeter` helpers.
- [x] Add `NestingQualityTests` and extend `NestingBenchmark` to report new metrics.
- [x] Generate `nesting-audit-report.md` and `nesting-current-algorithm-audit.md`.

**Verification:**
- `dotnet build src/NStCut.sln` — 0 warnings.
- `dotnet test src/NStCut.sln` — all tests pass.

---

## Stage 2 — PRO (Next 1–2 development cycles)

### 2.1 True contour area in reports and UI

**Problem:** `NestingResult.TotalUtilizationPercent` and `MainViewModel` still use bounding-box area, so non-rectangular parts report inflated utilization.

**Work:**
- Update `DxfGeometryService.ReadContour` to build a `Polygon2D` from closed `Polyline2D` entities and approximate arcs/circles with short line segments.
- Update `MainViewModel` to set `part.Contour = contour.Polygon` and `part.Area = contour.Polygon?.Area ?? contour.Bounds.Area`.
- Update `NestingResult.TotalUtilizationPercent` computation in `BoundingBoxShelfEngine` to use `PlacedPart.Source.Contour?.Area ?? placed.Bounds.Area`.
- Add reference tests that verify utilization drops when a part has a contour cutout.

**Files:**
- `src/NStCut.Dxf/Services/DxfGeometryService.cs`
- `src/NStCut.App/ViewModels/MainViewModel.cs`
- `src/NStCut.Nesting/Services/BoundingBoxShelfEngine.cs`

### 2.2 Better residual / free-rectangle analysis

**Problem:** `ResidualQualityScore` and `DeepPocketCount` are approximations based on individual free rectangles.

**Work:**
- Compute the union of all free rectangles on a sheet to detect true concave pockets.
- Add a "pocket visibility" check: a leftover region is valuable only if a rectangle of at least `RemnantMinUsefulWidth × RemnantMinUsefulLength` can be inscribed.
- Add `SheetInstance.Pockets` (or `ResidualZones`) with classification.
- Update `NestingQualityMetrics` to use the improved analysis.

**Files:**
- `src/NStCut.Core/Models/SheetInstance.cs`
- `src/NStCut.Core/Nesting/NestingQualityMetrics.cs`
- New helper: `src/NStCut.Core/Geometry/RectUnion.cs` (or similar)

### 2.3 Active small-part placement

**Problem:** The engine may leave small gaps unfilled because it sorts by global area/width.

**Work:**
- Add a "gap-filling" post-pass after the main pack: try to place remaining small parts into the best-fitting free rectangles (Best-Short-Side-Fit).
- Expose this as `NestingOptions.EnableGapFilling` (default `true`).
- Measure impact via `UnplacedFillability` and `ResidualQualityScore` in `NestingBenchmark`.

**Files:**
- `src/NStCut.Nesting/Services/BoundingBoxShelfEngine.cs`
- `src/NStCut.Core/Models/NestingOptions.cs`
- `src/NStCut.Core/Constants/AppConstants.cs`

### 2.4 Smarter multi-pass scheduling

**Problem:** 40 deterministic combinations may miss good layouts; many passes can be duplicates.

**Work:**
- Remember all `LayoutHash` values seen and skip duplicate permutations/strategy combos.
- Dynamically increase `MaxSearchPasses` when layout diversity is high and decrease when many duplicates occur.
- Add a "shake" pass that randomly perturbs part order (still deterministically seeded) after exhausting deterministic combos.

**Files:**
- `src/NStCut.Nesting/Services/MultiPassNestingEngine.cs`

### 2.5 UI quality dashboard

**Problem:** The operator cannot see why one layout was chosen over another.

**Work:**
- Add a "Quality" tab or tooltip in `SheetPreviewWindow`/`MainWindow` showing:
  - `ResidualQualityScore`
  - `UnplacedFillability`
  - `GeometricCompatibilityScore`
  - `TotalCutLength`
- Show the list of passes from `MultiPassNestingEngine.LastPassLog`.

**Files:**
- `src/NStCut.App/ViewModels/SheetPreviewViewModel.cs`
- `src/NStCut.App/Views/SheetPreviewWindow.xaml`

---

## Stage 3 — ADVANCED (Future / Research)

### 3.1 True-polygon nesting

**Problem:** Bounding-box packing is fundamentally inefficient for L-shapes, C-shapes, and parts with holes.

**Work:**
- Implement or integrate a polygon-packing / no-fit-polygon (NFP) algorithm.
- Support concave parts and holes.
- Allow `NestedInHole` placement.
- Keep the bbox engine as a fast fallback for simple rectangular parts.

**Files:**
- New project/namespace: `NStCut.Nesting.Geometry` or `NStCut.Nesting.Advanced`
- `src/NStCut.Nesting/Internals/TrueContourPacker.cs`

### 3.2 Local search / backtracking

**Problem:** Greedy sequential placement can get stuck in local optima.

**Work:**
- Add a post-optimization pass that tries to move/rotate each part locally while preserving feasibility.
- Implement a simple simulated-annealing or guided local search that accepts worse intermediate states to escape greedy traps (Scenario J).

**Files:**
- `src/NStCut.Nesting/Services/LayoutOptimizer.cs`

### 3.3 Multi-sheet optimization

**Problem:** `BoundingBoxShelfEngine` groups by material but does not optimize the distribution of parts across sheets globally.

**Work:**
- Add a bin-packing optimizer that assigns parts to sheets before placement to balance utilization.
- Support multiple materials in a single `Nest` call with global cross-material optimization.

### 3.4 Toolpath-aware nesting

**Problem:** `TotalCutLength` is measured but not optimized; lead-ins, common cuts, and cutting order are ignored.

**Work:**
- Add a `ToolpathEstimator` that computes estimated cutting time.
- Optionally prefer layouts with aligned edges that enable common-line cutting.

**Files:**
- `src/NStCut.Core/Nesting/ToolpathMetrics.cs`

---

## Stage 4 — Maintenance

- Keep `NestingBenchmark` running in CI and fail the build if reference scenarios regress.
- Periodically re-tune `AppConstants` residual/visual weights against real production data.
- Document any new quality criteria in `nesting-quality-specification.md` and update the objective function in `nesting-objective-function.md`.

---

## Risk & Dependency Notes

- **PRO 2.1** (true contour area) is safe and high-value; it only changes reported numbers, not placement.
- **PRO 2.2/2.3** change residual scoring and may alter which pass is selected; validate with `NestingBenchmark` diff.
- **ADVANCED 3.1** is a large change and should be developed behind a feature flag (`INestingEngine` factory) so the bbox engine remains the default until proven.
- **ADVANCED 3.2** can be non-deterministic if not carefully seeded; determinism must remain a hard constraint.

---

## History

- 2026-09-02 — Initial roadmap written after quality-specification audit.