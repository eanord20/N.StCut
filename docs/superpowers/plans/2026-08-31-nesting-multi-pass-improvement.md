# N.StCut — Multi-Pass Nesting improvement: implementation plan

> **For agentic workers:** execute task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Multi-Pass produce and select genuinely different bounding-box nesting layouts.

**Architecture:** `PackGroup` respects the permuted `StableIndex`; `PackStrategy` becomes a tie-break. `NestingResultComparison` becomes a multi-level comparator using `LayoutHash`, `UsedWidth`/`UsedHeight` and compactness. Pass log receives new metrics.

**Tech Stack:** .NET 8, C#.

---

### Task 1: LayoutHash

**Files:**
- Create: `src/NStCut.Core/Nesting/LayoutHash.cs`
- Modify: `src/NStCut.Core/Models/NestingResult.cs`, `src/NStCut.Core/Nesting/NestingPassResult.cs`

**Interfaces:**
- Consumes: `IEnumerable<SheetInstance>`
- Produces: `static string Compute(IReadOnlyList<SheetInstance> sheets)`

- [ ] **Step 1: Write deterministic hash helper**

```csharp
public static class LayoutHash
{
    public static string Compute(IReadOnlyList<SheetInstance> sheets)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var ms = new System.IO.MemoryStream();
        using (var writer = new System.IO.StreamWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            foreach (var sheet in sheets.OrderBy(s => s.Index))
            {
                foreach (var p in sheet.Parts.OrderBy(p => p.Source.Number)
                                              .ThenBy(p => p.CopyIndex)
                                              .ThenBy(p => p.Position.X)
                                              .ThenBy(p => p.Position.Y))
                {
                    writer.WriteLine($"{sheet.Index};{p.Source.Number};{p.CopyIndex};{p.Position.X:F6};{p.Position.Y:F6};{p.Bounds.Width:F6};{p.Bounds.Height:F6};{p.Rotation:F6}");
                }
            }
            writer.Flush();
        }
        var hash = sha.ComputeHash(ms.ToArray());
        return Convert.ToHexString(hash);
    }
}
```

- [ ] **Step 2: Add `LayoutHash` to `NestingResult`**

```csharp
public string LayoutHash { get; set; } = string.Empty;
```

- [ ] **Step 3: Add `LayoutHash` to `NestingPassResult`**

```csharp
public string LayoutHash { get; init; } = string.Empty;
```

**Acceptance:** `LayoutHash.Compute` returns the same string for the same layout and a different string if any part moves.

---

### Task 2: UsedWidth / UsedHeight

**Files:**
- Modify: `src/NStCut.Core/Models/SheetInstance.cs`
- Modify: `src/NStCut.Nesting/Internals/MaxRectsPacker.cs`

**Interfaces:**
- Consumes: `List<PlacedPart>`
- Produces: `SheetInstance.UsedWidth`, `SheetInstance.UsedHeight`

- [ ] **Step 1: Add fields**

```csharp
public double UsedWidth { get; set; }
public double UsedHeight { get; set; }
```

- [ ] **Step 2: Compute in `MaxRectsPacker.ToSheet()`**

After `sheet.Parts.AddRange(Parts)` add:

```csharp
sheet.UsedWidth = Parts.Count > 0 ? Parts.Max(p => p.Bounds.Right) : 0;
sheet.UsedHeight = Parts.Count > 0 ? Parts.Max(p => p.Bounds.Top) : 0;
```

**Acceptance:** Existing `MaxRectsPackerTests` still pass.

---

### Task 3: PackGroup respects permutation

**Files:**
- Modify: `src/NStCut.Nesting/Services/BoundingBoxShelfEngine.cs`

**Interfaces:**
- Consumes: `List<(int CopyIndex, Part Part, int StableIndex)>`
- Produces: re-ordered `sorted` list

- [ ] **Step 1: Swap order keys**

Replace the `strategy switch` in `PackGroup` from:

```csharp
var ordered = strategy switch
{
    PackingStrategy.MaxSide => copies.OrderByDescending(x => Math.Max(x.Part.Bounds.Width, x.Part.Bounds.Height)),
    ...
};
var sorted = ordered.ThenBy(x => x.StableIndex).ThenBy(x => x.CopyIndex).ToList();
```

to:

```csharp
var ordered = copies.OrderBy(x => x.StableIndex).ThenBy(x => x.CopyIndex);
var ordered = strategy switch
{
    PackingStrategy.MaxSide => ordered.ThenByDescending(x => Math.Max(x.Part.Bounds.Width, x.Part.Bounds.Height)),
    PackingStrategy.Area => ordered.ThenByDescending(x => x.Part.Bounds.Area),
    PackingStrategy.Width => ordered.ThenByDescending(x => x.Part.Bounds.Width),
    PackingStrategy.Height => ordered.ThenByDescending(x => x.Part.Bounds.Height),
    _ => throw new ArgumentOutOfRangeException(nameof(strategy))
};
var sorted = ordered.ToList();
```

**Acceptance:** `MultiPass` with different permutations now produces different `LayoutHash` values for multi-size parts.

---

### Task 4: Multi-level comparison

**Files:**
- Modify: `src/NStCut.Core/Nesting/NestingResultComparison.cs`

**Interfaces:**
- Consumes: `NestingResult` A and B
- Produces: `bool IsBetter(A, B)`

- [ ] **Step 1: Lexicographic comparator**

```csharp
public static bool IsBetter(NestingResult candidate, NestingResult current)
{
    if (candidate.Unplaced.Count != current.Unplaced.Count)
        return candidate.Unplaced.Count < current.Unplaced.Count;

    if (candidate.Sheets.Count != current.Sheets.Count)
        return candidate.Sheets.Count < current.Sheets.Count;

    var utilCmp = candidate.TotalUtilizationPercent.CompareTo(current.TotalUtilizationPercent);
    if (utilCmp != 0) return utilCmp > 0;

    var usedA = candidate.Sheets.Sum(s => s.UsedWidth * s.UsedHeight);
    var usedB = current.Sheets.Sum(s => s.UsedWidth * s.UsedHeight);
    var usedCmp = usedA.CompareTo(usedB);
    if (usedCmp != 0) return usedCmp < 0;

    var maxDimA = candidate.Sheets.Sum(s => Math.Max(s.UsedWidth, s.UsedHeight));
    var maxDimB = current.Sheets.Sum(s => Math.Max(s.UsedWidth, s.UsedHeight));
    var maxCmp = maxDimA.CompareTo(maxDimB);
    if (maxCmp != 0) return maxCmp < 0;

    return string.Compare(candidate.LayoutHash, current.LayoutHash, StringComparison.Ordinal) < 0;
}
```

**Acceptance:** `NestingResultComparisonTests` still pass; new test shows a more compact one-sheet layout wins over a less compact one with the same utilization.

---

### Task 5: Fill pass log and result metadata

**Files:**
- Modify: `src/NStCut.Nesting/Services/MultiPassNestingEngine.cs`
- Modify: `src/NStCut.Nesting/Services/BoundingBoxShelfEngine.cs`

- [ ] **Step 1: Compute and store `LayoutHash` on each candidate**

In `BoundingBoxShelfEngine.NestCore`, after `result.TotalUtilizationPercent` is set:

```csharp
result.LayoutHash = LayoutHash.Compute(result.Sheets);
```

- [ ] **Step 2: Extend `NestingPassResult` log in `MultiPassNestingEngine`**

```csharp
log.Add(new NestingPassResult
{
    ...
    LayoutHash = candidate.LayoutHash,
    UsedBoundingArea = candidate.Sheets.Sum(s => s.UsedWidth * s.UsedHeight)
});
```

- [ ] **Step 3: Assign `best.LayoutHash` to final result**

Already handled by the `Compute` call in `BoundingBoxShelfEngine`.

**Acceptance:** `Nest_WritesLogFile` test is updated to assert `LayoutHash` is non-empty.

---

### Task 6: Verification / test

**Files:**
- Modify: `src/NStCut.Nesting.Tests/MultiPassNestingEngineTests.cs`

- [ ] **Step 1: Add diagnostic test**

New test `Nest_ProducesMultipleUniqueLayouts` with a 6-part set, `MaxSearchPasses >= 16`, asserts:

- `uniqueHashes.Count > 1`
- `best.LayoutHash` equals the hash of the result with the best lexicographic score
- `best` is not always the first pass

- [ ] **Step 2: Run `dotnet test`**

```bash
dotnet test src/NStCut.sln --no-build
```

Expected: all tests pass, no warnings.

---

### Task 7: UI hash check

**Files:**
- Modify: `src/NStCut.App.Tests/MainViewModelTests.cs` or `src/NStCut.Nesting.Tests`

- [ ] **Step 1: Add `SheetPreviewItemViewModel` hash check**

Verify that for a given `SheetInstance`, `PreviewPartViewModel` coordinates reconstruct a hash matching the source `SheetInstance`.

**Acceptance:** `best LayoutHash` equals `preview LayoutHash` for the same sheet.

---

## Global constraints

- No hardcoded values: use `AppConstants.ComparisonResolution` and `NestingOptions` values.
- No UI refactoring: `SheetPreviewWindow` and `MainWindow` are unchanged unless logging UI is explicitly requested later.
- Preserve deterministic behavior: all secondary sorts are stable and based on `StableIndex`/`LayoutHash`.
