# N.StCut — Multi-Pass Nesting: design

## Goal
Make the Multi-Pass search produce and select genuinely different bounding-box nesting layouts, instead of repeatedly arriving at the same first-pass layout.

## Current root cause (Phase A confirmed)

- `MultiPassNestingEngine` permutes the part list via `DeterministicPermutationSource.Permute()`.
- `BoundingBoxShelfEngine.PackGroup()` immediately re-sorts the same copy list by `PackStrategy` (`MaxSide`, `Area`, `Width`, `Height`) and uses `StableIndex` only as a `ThenBy` tie-break.
- For parts of different sizes the geometry sort dominates, so the permutation is effectively erased and many passes generate the same layout.
- `NestingResultComparison.IsBetter()` compares only `Unplaced`, `Sheets`, and `TotalUtilizationPercent`. Different layouts with identical metrics are treated as equal, and the first candidate is kept.

## Proposed architecture

### 1. Permutation is the primary order

In `PackGroup`, `StableIndex` (which already encodes the MultiPass permutation) becomes the primary sort key. `PackStrategy` values become the tie-break for parts of equal size. Existing strategies are preserved; they are no longer allowed to override the permutation.

### 2. Layout hash

Add a deterministic `LayoutHash` to `NestingPassResult` and `NestingResult`, computed from:

```text
for each sheet:
  for each placed part sorted by (Number, CopyIndex, SheetIndex):
    Number
    CopyIndex
    Position.X
    Position.Y
    Bounds.Width
    Bounds.Height
    Rotation
    Sheet.Index
```

This allows unambiguous identity of a layout.

### 3. Multi-level scoring

Replace the flat `NestingResultComparison` with a lexicographic comparison:

1. Fewer `Unplaced` parts.
2. Fewer `Sheets`.
3. Higher `TotalUtilizationPercent`.
4. Smaller total `UsedBoundingArea` (sum of `usedWidth × usedHeight` per sheet).
5. Better `Compactness` (larger minimum remaining rectangle, or smaller `max(usedWidth, usedHeight)`).
6. Stable deterministic tie-break using `LayoutHash`.

### 4. Used bounding area

Add `UsedWidth` and `UsedHeight` to `SheetInstance`, computed from `PlacedPart.Bounds` after packing. This gives a secondary quality signal that distinguishes two one-sheet layouts with the same utilization.

### 5. Pass log enrichment

`MultiPassNestingEngine` already keeps `LastPassLog`. Extend `NestingPassResult` with `LayoutHash`, `UsedBoundingArea`, and `Compactness`.

### 6. UI scope

`SheetPreviewViewModel` and `SheetPreviewWindow` are not modified. The verification only needs to confirm that the `best` result's `LayoutHash` equals the hash of the rendered `PreviewPartViewModel` set.

## Success criteria

- `MultiPass` produces more than one unique `LayoutHash` for a multi-size part set.
- The selected `best` is not always the first pass.
- `best` is rendered exactly as computed (`best LayoutHash == preview LayoutHash`).
- Results remain deterministic for the same input.
- Existing tests still pass.
