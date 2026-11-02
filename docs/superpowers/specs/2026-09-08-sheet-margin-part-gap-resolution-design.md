# Sheet Margin, Part Gap, and Resolution Design

**Date:** 2026-09-08  
**Roadmap:** NSC-003  
**Status:** Implemented and verified

## Goal

Define one unambiguous geometry contract for sheet margins, spacing between parts, and numerical resolution. The contract must be shared by fit evaluation, nesting, validation, preview, and export.

## Terms

- `SheetMarginMm` is the minimum physical distance from every sheet edge to every placed part bounding box.
- `PartGapMm` is the minimum physical distance between bounding boxes of different placed parts.
- `Resolution` is a numerical comparison and geometry-cleanup threshold. It is not physical clearance.
- The usable sheet rectangle is the physical sheet after subtracting `SheetMarginMm` from all four edges.

## Hard Constraints

For a sheet with `Length = L`, `Width = W`, and margin `M`:

- `usableWidth = L - 2M`
- `usableHeight = W - 2M`
- every placed part must satisfy:
  - `Bounds.Left >= M`
  - `Bounds.Bottom >= M`
  - `Bounds.Right <= L - M`
  - `Bounds.Top <= W - M`
- two different placed parts `A` and `B` are valid only when at least one separation condition is true:
  - `A.Right + PartGapMm <= B.Left`
  - `B.Right + PartGapMm <= A.Left`
  - `A.Top + PartGapMm <= B.Bottom`
  - `B.Top + PartGapMm <= A.Bottom`

The constraints above are strict physical constraints. `Resolution` must not permit a part to cross the usable sheet boundary or reduce `PartGapMm`.

## Sheet Margin Semantics

`SheetMarginMm` applies equally to the left, right, bottom, and top edges.

`PartGapMm` does not add clearance between a part and a sheet edge. A part may touch the usable rectangle boundary exactly. Therefore, a single part whose oriented bounding box is exactly `usableWidth × usableHeight` fits even when `PartGapMm > 0`.

Negative margin input is normalized to zero at the application boundary. A margin that leaves zero or negative usable width or height makes the sheet unusable for placement.

## Zero-Margin Contract

`SheetMarginMm = 0` is valid and must not be treated as missing or invalid configuration.

With zero margin:

- the usable rectangle is exactly `[0, L] × [0, W]`;
- placed coordinates may equal zero;
- no generated free rectangle or physical placed-part bound may have a negative coordinate or negative size;
- a part of size exactly `L × W` fits when its orientation policy allows that orientation, regardless of `PartGapMm`;
- a part with width greater than `L` or height greater than `W` does not fit, even when the excess is less than or equal to `Resolution`;
- evaluator and packer must return the same fit decision;
- preview and DXF export must preserve placement at `(0, 0)` without introducing an artificial offset.

Loading persisted settings must preserve an explicitly stored zero margin. Zero must not be replaced by the default value.

## Part Gap Semantics

`PartGapMm` applies only between different parts. It is not kerf and is not split into half-gaps stored on each physical part.

Zero gap is valid. With `PartGapMm = 0`, touching part bounding boxes are allowed, but overlap is not.

Negative gap input is normalized to zero at the application boundary.

## Resolution Semantics

`Resolution` may be used only for:

- deciding whether width and height are numerically equivalent for orientation deduplication;
- comparing or deduplicating free rectangles;
- removing numerically insignificant free rectangles;
- stabilizing deterministic equality and tie-breaking where no physical constraint is weakened.

`Resolution` must not be used to:

- allow a placed bound beyond the usable sheet rectangle;
- reduce the required spacing between parts;
- change quantity;
- snap exported coordinates outside the validated layout;
- hide invalid, negative, NaN, or infinite dimensions.

`Resolution = 0` is valid and means exact comparisons. Negative resolution is normalized to zero.

## Recommended MaxRects Representation

Keep the existing deterministic effective-bound representation:

- `effectiveWidth = partWidth + PartGapMm`
- `effectiveHeight = partHeight + PartGapMm`

Represent the virtual packing area as:

- `virtualWidth = usableWidth + PartGapMm`
- `virtualHeight = usableHeight + PartGapMm`

The extra virtual strip exists only to avoid applying `PartGapMm` at the right and top sheet boundaries. It is not physical sheet area. Physical `PlacedPart.Bounds` remain unexpanded and must satisfy the hard sheet constraints.

This representation preserves a full gap between neighboring effective rectangles while allowing the last physical part to touch the right or top usable boundary. Left and bottom positions remain offset only by `SheetMarginMm`.

All placement paths, including `TryPlace`, `TryPlaceAt`, `PlaceExisting`, repacking, gap filling, and per-sheet renesting, must use the same representation.

## Fit Evaluation

`SheetFitEvaluator` checks each allowed physical orientation directly:

- `orientation.Width <= usableWidth`
- `orientation.Height <= usableHeight`

It must not add `PartGapMm` for a single-part fit decision. The evaluator answers whether one physical part can fit an empty usable sheet. Inter-part spacing is enforced by the packer when multiple parts are placed.

The evaluator must reject invalid dimensions and must agree with the packer for an empty sheet.

## Determinism

The change must not alter deterministic ordering or tie-breaking. Identical inputs and options must produce identical layouts and hashes across repeated runs.

Changing the margin/gap contract may legitimately change existing layout hashes. Canonical hashes must be regenerated only after the new contract tests pass.

## Validation and Error Handling

- `SheetMarginMm`, `PartGapMm`, and `Resolution` must be finite and nonnegative after normalization.
- Sheet and part dimensions must be finite and strictly positive before placement.
- A sheet with nonpositive usable dimensions accepts no parts and must not create a nonempty result sheet.
- Invalid values must not produce NaN/Infinity coordinates, negative rectangles, or exceptions from ordinary fit evaluation.

## Acceptance Tests

### Margin

1. A `100 × 50` part fits exactly on a `100 × 50` sheet with margin `0` and any nonnegative gap.
2. A `98 × 48` part fits exactly on a `100 × 50` sheet with margin `1`.
3. A `98.001 × 48` part does not fit with margin `1`, even when `Resolution >= 0.001`.
4. Placements with margin `M` may touch each of the four usable boundaries exactly.
5. A margin producing zero or negative usable width/height yields no placement.

### Zero margin

6. A full-sheet part is placed at `(0, 0)` with no negative free rectangles.
7. Evaluator and packer both accept exact full-sheet fit at margin `0`.
8. Evaluator and packer both reject a part exceeding the sheet by any positive amount.
9. Persisted `SheetMarginMm = 0` round-trips and is restored as zero.

### Gap

10. One full-sheet part fits when gap is greater than zero.
11. Two parts preserve at least the full configured gap.
12. Gap is not required between a part and any usable sheet boundary.
13. With gap `0`, touching is accepted and overlap is rejected.
14. Resolution never permits a gap smaller than configured.

### Consistency and determinism

15. `SheetFitEvaluator` and an empty `MaxRectsPacker` agree for all tested orientations, margins, gaps, and resolutions.
16. `TryPlaceAt`, repack, gap filling, and normal placement satisfy the same physical validator.
17. Repeated runs produce identical positions, rotations, sheet assignment, and layout hash.

## Scope

Included:

- geometry contract and tests;
- `SheetFitEvaluator` consistency;
- MaxRects usable/effective-area correction;
- zero-margin persistence regression;
- physical layout validation for all placement paths.

Excluded:

- polygon/NFP nesting;
- kerf/toolpath compensation;
- UI redesign;
- changing default margin, gap, or resolution values;
- allowing tolerance-based boundary or gap violations.
