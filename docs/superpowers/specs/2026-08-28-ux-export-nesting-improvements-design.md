# N.StCut UX, Export and Nesting Improvements Design

## Goal

Implement ten requested improvements to table interaction, file dialogs, persisted paths, material management, preview sizing, export names, and deterministic nesting quality while preserving existing behavior and current uncommitted work where it is correct.

## UI behavior

- Main and material table checkboxes use one explicit high-contrast application style in both light and dark themes.
- `CandidateSelectWindow` opens centered on its owner, uses a wider adaptive size bounded by the monitor work area, and lets long paths remain accessible through scrolling and tooltips.
- The DXF path can be copied from the table through a context-menu command and the standard copy gesture for the selected path cell.
- Explorer drag-and-drop accepts DXF files only. Dropping one DXF on a row assigns it to that row. Dropping one or more DXFs on empty table space creates rows, deriving number/title from each filename. Both operations participate in Undo.
- Materials can be duplicated in the library. A duplicate receives a new ID and the first available numeric name suffix: `Material_1`, `Material_2`, and so on.
- `SheetPreviewWindow` opens centered on the main window at approximately 90% of the current monitor work area, but remains a normal, resizable, non-maximized window.

## Persisted paths

Only the two main folders are persisted:

- DXF search folder (`SearchPath`);
- output folder (`OutputPath`).

The existing application settings remain the source of truth. Folder dialogs initialize from the corresponding persisted path, and a successful selection is saved immediately. Invalid or missing saved paths do not prevent opening a dialog or starting the application.

## Export naming

A shared formatter generates Windows-safe filenames and replaces invalid filename characters with `_`.

DXF files use:

`Material_LengthxWidth_Sheet-01_yyyyMMdd.dxf`

Reports use:

`yyyyMMdd_HHmmss.docx`

One timestamp is captured per export operation, ensuring all sheets exported in one batch have the same date. Existing files are not silently overwritten; a deterministic numeric suffix is added when needed.

## Nesting

The current deterministic bounding-box shelf architecture is retained.

1. Correct the placement scoring defect where creating a new shelf always receives zero waste and can incorrectly beat reuse of an existing shelf.
2. Run a fixed set of deterministic orderings: descending maximum side, area, width, and height.
3. Compare complete candidate results by, in order: fewer unplaced copies, fewer sheets, greater placed utilization, then fixed strategy order.
4. Preserve quantity, grain restrictions, 0/90-degree rotation rules, margins, gaps, precision, and absolute sheet bounds.

This improves the current shelf result without claiming full reuse of arbitrary internal rectangular voids. MaxRects or polygon/NFP nesting remains a separate future architectural change.

## Error handling

- Non-DXF drops are rejected without modifying rows.
- Missing/empty DXF paths disable copying.
- A failed dropped-file geometry read leaves the row in the existing unresolved/error flow rather than losing the path silently.
- Invalid export-name components are sanitized; an empty sanitized material name falls back to an existing localized/default material label.
- Settings persistence keeps the existing tolerant behavior for missing or malformed JSON.

## Combined DXF export mode and dark-theme refinement

- `Combined DXF` is a checkable option, not a separate export action. Its state is persisted in application settings and restored on the next launch.
- The existing `Export DXF` button is the single entry point: when the option is enabled it writes one timestamp-named combined DXF; otherwise it writes one DXF per sheet with the existing names.
- Grain direction annotations are present in both individual and combined DXF exports. Labels and arrows are placed entirely outside sheet boundaries on dedicated `SheetLabels` and `GrainArrow` layers, so no annotation geometry appears within the cutting area.
- Combined layout reserves an annotation band around every row, preventing labels/arrows from intersecting any sheet or another row.
- Preview and report visualization remain unchanged because they are not production cutting files.
- Light and dark themes define explicit application menu/input surface and foreground brushes. `Menu`, `MenuItem`, the material `ComboBox`, and its dropdown items use matching contrast pairs rather than relying on incompatible MaterialDesign defaults.

## Testing

- Unit-test unique material suffix generation and new IDs.
- Unit-test export filename formatting, sanitization, timestamp consistency, and collision suffixes.
- Unit-test dropped-file mapping separately from WPF event plumbing.
- Add nesting regression tests proving existing shelves are reused, candidate ordering improves or matches the baseline, quantities are unchanged, grain constraints hold, and repeated runs are identical.
- Run all solution tests and a Release build.
- Manually verify checkbox contrast in both themes, adaptive windows on multiple resolutions, copy behavior, and Explorer drag-and-drop.

## Scope and compatibility

- EN/UA localization remains unchanged in scope; no RU resources are introduced.
- Existing project files and settings remain backward compatible because new behavior reuses existing persisted path properties.
- Current uncommitted changes are compared with `HEAD`; correct work is retained rather than overwritten.
