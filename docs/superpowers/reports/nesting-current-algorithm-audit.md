# Nesting Algorithm Audit — N.StCut

**Date:** 2026-09-02
**Engine:** `MultiPassNestingEngine` → `BoundingBoxShelfEngine` (`MaxRectsPacker`)
**Audit basis:** source code, `NestingBenchmark` report (`docs/superpowers/reports/nesting-audit-report.md`), `NestingQualityTests`

---

## 1. Executive Summary

This audit compares the existing nesting implementation against the quality criteria defined in `docs/superpowers/specs/nesting-quality-specification.md`.

**Key finding:** the scoring/comparison layer has been updated to use a multi-level objective function (`NestingQualityScore`), but the **packing core is still a bounding-box MaxRects algorithm** with a deterministic multi-pass search. It does not understand real part contours, holes, or concave leftovers, and it cannot place a part inside the void of another part.

**Overall verdict:**
- Hard constraints (no overlap, sheet bounds, grain, quantity) are enforced.
- Primary/secondary objectives (sheets, utilization, compactness, residual quality, contact length, cut length) are now measured and compared.
- Advanced features (nested-in-hole, true-contour packing, deep-pocket exact detection, toolpath-level cut optimization) are **not supported**.

---

## 2. What the algorithm currently considers a "good nesting"

The production selection logic is now implemented in `NestingResultComparison.IsBetter` → `NestingQualityMetrics.Compute` → `NestingQualityScore.IsBetterThan`.

The lexicographic order is:

1. `UnplacedCount` (lower)
2. `SheetCount` (lower)
3. `TrueUtilizationPercent` (higher)
4. `UsedBoundingArea` (lower)
5. `MaxUsedDimensionSum` (lower)
6. `ResidualQualityScore` (higher)
7. `UnplacedFillability` (lower)
8. `GeometricCompatibilityScore` (higher)
9. `TotalCutLength` (lower)
10. `VisualQualityScore` (higher)
11. `LayoutHash` (deterministic tie-break)

Before this audit/implementation cycle, the comparison was a 5-level lexicographic score (`Unplaced` → `Sheets` → `Utilization` → `UsedBoundingArea` → `MaxDim` → `LayoutHash`). The new score adds residual-space quality, fillability, geometric compatibility, cut length, and a composite visual tie-break.

---

## 3. Criterion Audit Matrix

| Criterion | Class | Status | Code Location | Notes |
|-----------|-------|--------|---------------|-------|
| No overlap | Hard | Implemented | `MaxRectsPacker.TryPlace`, `SplitFreeRectangles` | Uses `effectiveWidth/Height = width/height + PartGap`. |
| Sheet bounds | Hard | Implemented | `MaxRectsPacker` constructor, `AddIfValid` | Only places inside `Material.Length - 2*Margin`. |
| Part gap / margin | Hard | Implemented | `MaxRectsPacker` (`_gap`, `_margin`) | `NestingComparison.FitsWithin` is `value <= limit`. |
| Quantity preservation | Hard | Implemented | `BoundingBoxShelfEngine.NestCore` expands `Quantity` and clones unplaced as 1 each. | |
| Grain / rotation | Hard | Implemented | `OrientationPolicy.GetOrientations` | `GrainMode.FollowMaterial` blocks rotation when `HasGrain`. |
| Determinism | Hard | Implemented | `StableHash`, `LayoutHash`, deterministic sort keys | Same input → same output. |
| Sheet count | Primary | Implemented | `NestingQualityScore` (and `BoundingBoxShelfEngine.GroupCandidate.IsBetter`) | First primary objective after unplaced. |
| Utilization | Primary | Implemented | `NestingQualityMetrics.Compute` uses `PlacedPart.Bounds.Area` or `Source.Contour.Area`. | `result.TotalUtilizationPercent` is still bbox-based; `TrueUtilizationPercent` uses contours when available. |
| Compactness | Secondary | Implemented | `NestingQualityMetrics.Compute` → `UsedBoundingArea`, `MaxUsedDimensionSum` | Derived from `SheetInstance.UsedWidth`/`UsedHeight`. |
| Residual quality | Secondary | Implemented | `NestingQualityMetrics.Compute` | Classifies free rectangles into valuable/useful and penalizes fragmentation/long-thin/deep-pocket. Uses rectangle-union area to avoid double counting overlapping MaxRects free rectangles. |
| Void fillability | Secondary | Implemented | `NestingQualityMetrics.ComputeUnplacedFillability` | Counts unplaced parts that would fit in existing free rectangles. |
| Small-part integration | Secondary | Partial | Measured indirectly via `UnplacedFillability` and `ResidualQualityScore`. | No explicit "small parts first" ordering beyond pack strategies. |
| Geometric compatibility | Secondary | Implemented | `NestingQualityMetrics.ComputeSheetContactLength` | Computes shared edge length between adjacent parts. |
| Nested-in-hole | Heuristic | Not implemented | — | `Part.Contour` exists but is never used for placement; engine is bbox-only. |
| Remnant quality | Secondary | Implemented | `NestingQualityMetrics.AnalyzeFreeRectangles` | `SheetInstance.FreeRectangles` populated from `MaxRectsPacker`. |
| Production efficiency (cut length) | Secondary | Implemented | `NestingQualityMetrics.ComputeSheetCutLength` | Uses contour perimeter when available, else bounding-box perimeter. |
| Visual quality | Heuristic | Implemented | `NestingQualityMetrics.ComputeVisualQualityScore` | Composite tie-breaker. |
| Geometric density | Secondary | Partial | `Compactness` (`TotalPartArea / UsedBoundingArea`) is reported in visual score. | No separate "density" metric. |
| Long-thin waste | Secondary | Implemented | `NestingQualityMetrics.AnalyzeFreeRectangles` | Uses `AppConstants.LongThinWaste*` thresholds. |
| Deep pockets | Secondary | Implemented (approximation) | `NestingQualityMetrics.AnalyzeFreeRectangles` | Approximated by free-rectangle area + min dimension; true concave pockets require union/visibility analysis. |

---

## 4. Code Mapping

| Quality concept | Files | Key types/methods |
|-------------------|-------|-------------------|
| Objective function | `src/NStCut.Core/Nesting/NestingResultComparison.cs` | `IsBetter` |
| Score computation | `src/NStCut.Core/Nesting/NestingQualityMetrics.cs` | `Compute` |
| Score vector | `src/NStCut.Core/Nesting/NestingQualityScore.cs` | `NestingQualityScore`, `IsBetterThan` |
| Pass selection | `src/NStCut.Nesting/Services/MultiPassNestingEngine.cs` | `Nest`, uses `NestingResultComparison.IsBetter` |
| Strategy selection | `src/NStCut.Nesting/Services/BoundingBoxShelfEngine.cs` | `GroupCandidate.IsBetter` (also uses `NestingResultComparison`) |
| Packer | `src/NStCut.Nesting/Internals/MaxRectsPacker.cs` | `TryPlace`, `ToSheet` |
| Free rectangles | `src/NStCut.Core/Models/SheetInstance.cs`, `MaxRectsPacker.ToSheet` | `FreeRectangles` list |
| Orientation / grain | `src/NStCut.Core/Nesting/OrientationPolicy.cs` | `GetOrientations` |
| Layout identity | `src/NStCut.Core/Nesting/LayoutHash.cs` | `Compute` |
| Benchmark / metrics report | `src/NStCut.Nesting.Tests/NestingBenchmark.cs`, `docs/superpowers/reports/nesting-audit-report.md` | `RunAllScenarios_AndWriteAuditReport` |
| Reference tests | `src/NStCut.Nesting.Tests/NestingQualityTests.cs` | `Reference_*` and `Metrics_*` tests |
| Geometry helpers | `src/NStCut.Core/Geometry/Polygon2D.cs` | `Area`, `Perimeter` (for future contour use) |

---

## 5. Benchmark Findings (selected)

Latest benchmark report (`nesting-audit-report.md`, generated 2026-09-02) summary for the 10 reference scenarios:

| Case | Sheets | Unplaced | Util % | Residual Q | Fillability | Geom | Visual |
|------|--------|----------|--------|------------|-------------|------|--------|
| A | 1 | 0 | 82.82 | 0.0212 | 0 | 0.1714 | 0.5113 |
| B | 2 | 0 | 45.55 | 0.4112 | 0 | 0.2136 | 0.5451 |
| C | 1 | 0 | 51.76 | 0.3842 | 0 | 0.1946 | 0.6086 |
| D | 2 | 0 | 46.58 | 0.4186 | 0 | 0.1804 | 0.5063 |
| E | 1 | 0 | 51.76 | 0.3734 | 0 | 0.1092 | 0.4407 |
| F | 4 | 0 | 69.01 | 0.1775 | 0 | 0.2222 | 0.5617 |
| G | 2 | 0 | 51.41 | 0.3044 | 0 | 0.1693 | 0.5299 |
| H | 1 | 0 | 40.72 | 0.5068 | 0 | 0.0934 | 0.4705 |
| I | 1 | 0 | 21.39 | 0.7393 | 0 | 0.1043 | 0.4999 |
| J | 1 | 0 | 44.51 | 0.4378 | 0 | 0.1196 | 0.4763 |

Observations:
- All reference scenarios A–J are placed with 0 unplaced parts.
- `Fillability` is 0 in all cases: no unplaced part could fit in a leftover free rectangle.
- For cases H and I (L-shape / C-shape) the `Util %` drops dramatically because the benchmark uses `Source.Contour.Area` for true utilization, while the engine still treats the parts as solid bounding boxes. This directly shows the **bbox-only gap**.
- `Geom` (contact length ratio) is relatively low (< 0.23) even for good packings; this is expected because MaxRects leaves `PartGap` between all parts and the metric is a ratio to total part perimeter.

---

## 6. Missing Features & Gaps

| # | Gap | Consequence | Where it matters |
|---|-----|-------------|------------------|
| 1 | **True contour packing** | `Part.Contour` is computed by `DxfGeometryService` as `null` and never used for placement. Parts are always rectangles. | L-shapes, notches, arcs, holes. |
| 2 | **Nested-in-hole** | A small part can never be placed inside the bounding-box void of a larger part. | Sheet utilization for parts with holes/cutouts. |
| 3 | **Deep pocket / visibility analysis** | `DeepPocketCount` is only an approximation on free rectangles. True concave leftover pockets are not detected. | Residual quality scoring. |
| 4 | **Small-part ordering** | The engine sorts by `PackingStrategy` (area/max-side/etc.); it does not actively reserve small gaps for small parts. | Scenarios with mixed sizes (B, G, J). |
| 5 | **No backtracking / local search** | `MultiPass` only permutes input order; it never reorders already placed parts. | Greedy traps (J). |
| 6 | **Limited pass space** | 4 heuristics × 2 orientations × 5 strategies = 40 deterministic combos. May miss layouts requiring different search. | Complex cases. |
| 7 | **Cut-length optimization** | `TotalCutLength` is measured but not optimized; toolpath ordering is out of scope. | Production time. |
| 8 | **ShelfPacker unused** | `ShelfPacker` exists but `BoundingBoxShelfEngine` always creates `MaxRectsPacker`. | Potential alternative strategy lost. |
| 9 | **UI reports use bbox** | `NestingResult.TotalUtilizationPercent` is still computed from `PlacedPart.Bounds.Area`. Reports may overstate utilization for non-rectangular parts until `DxfGeometryService` populates `Contour` and `MainViewModel` uses it. | User-visible metrics. |

---

## 7. Root Causes of Visual Mismatch

A layout can look bad to an operator while still scoring high on utilization because the current engine:

1. **Packs by bounding box**, not by real shape. Concave voids are treated as occupied space, creating wasted area inside the bbox of L/C-shaped parts.
2. **Builds a single sequence** and never reopens a placed part. Once a part is placed, its position is final for that pass.
3. **Uses MaxRects free-rectangle splitting**, which can fragment leftover space into many narrow rectangles, especially after large parts are placed early.
4. **Does not plan for small parts**. The sort keys are global (area, width, etc.), not adaptive to the remaining cavity sizes.
5. **Treats orientation and pack strategy as independent axes** in `MultiPass`, which can miss combined effects (e.g., a part that should be rotated AND moved to a different sheet).

---

## 8. Q&A (answers to the audit questions)

### Q1. What does the algorithm currently consider a good nesting?
A good nesting is one that, in order of priority: places all parts, uses the fewest sheets, maximizes true part area used, minimizes the used bounding area, minimizes the maximum used dimension, leaves high-quality residuals, does not miss fillable voids, packs parts tightly, minimizes cut length, and has good visual balance.

### Q2. Where is "good" implemented?
In `src/NStCut.Core/Nesting/NestingResultComparison.cs` (selection), `src/NStCut.Core/Nesting/NestingQualityMetrics.cs` (metrics), and `src/NStCut.Core/Nesting/NestingQualityScore.cs` (lexicographic comparison).

### Q3. Which criteria from the quality specification are not implemented?
True contour/hole nesting and nested-in-hole placement are not implemented. Deep-pocket detection is an approximation. Small-part integration is only measured, not actively optimized by the packer.

### Q4. Why does the same multi-pass run sometimes produce duplicate hashes?
Because `BoundingBoxShelfEngine.PackGroup` re-sorts by part size for some strategies, the permutation from `DeterministicPermutationSource` can be overwritten. This was already addressed by making `StableIndex` the primary sort key; see `BoundingBoxShelfEngine.cs`.

### Q5. Is the score deterministic?
Yes. All comparisons use deterministic inputs and `LayoutHash` is a deterministic SHA-256 of sorted placed-part data.

### Q6. Does the score match what a human would call "good"?
Partially. Sheet count and compactness match. Residual quality and visual score approximate human judgement, but they cannot compensate for the lack of true-contour packing.

### Q7. Which metrics are most sensitive to small layout changes?
`UsedBoundingArea` and `MaxUsedDimensionSum` move quickly with part positions. `ResidualQualityScore` is sensitive to how free rectangles are split. `GeometricCompatibilityScore` is sensitive to adjacency.

### Q8. What is the biggest low-effort improvement?
Populate `Part.Contour` from DXF and use `NestingQualityMetrics.TrueUtilizationPercent` consistently. This alone fixes the over-reported utilization for non-rectangular parts.

### Q9. What is the biggest high-effort improvement?
Replace or augment the bbox packer with a true-polygon nesting algorithm that supports holes/concavities and nested-in-hole placement.

### Q10. Is `MultiPass` actually exploring different layouts?
Yes, the benchmark shows multiple unique `LayoutHash` values per case (5–28 depending on the scenario). However, the search is limited to 40 deterministic combinations.

### Q11. Are the new metrics stable enough for production?
The metrics are deterministic and additive, but their weights/thresholds (`AppConstants` residual/visual weights) should be validated against real production layouts before freezing.

---

## 9. Conclusion

The N.StCut nesting engine now has a **formal, measurable quality model** that is integrated into both pass selection and strategy selection. The main remaining limitation is the **bounding-box packing core**, which is a separate, larger architectural improvement. The recommended next steps are documented in `nesting-improvement-roadmap.md`.

---

## History

- 2026-09-02 — Initial audit after implementing `NestingQualityMetrics` / `NestingQualityScore`.