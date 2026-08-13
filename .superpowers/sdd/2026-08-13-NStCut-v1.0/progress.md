# SDD ledger — plan: docs/superpowers/plans/2026-08-13-NStCut-v1.0-master.md

## Global Constraints

- Target framework: `net8.0` for libraries, `net8.0-windows` for WPF.
- All dimensions in millimeters.
- Default `PartGapMm = 5.0` and `SheetMarginMm = 5.0`, never hardcoded.
- Localization resources only EN/UA; no Russian resources.
- DXF matching by filename using NS_PDF_Merge normalization rules.
- No magic numbers/colors in logic or UI; use `AppConstants`, `NestingOptions`, `AppTheme.xaml`.
- No secrets or local paths committed; `.gitignore` must cover `bin/`, `obj/`, `.vs/`, `*.user`.
- Services behind interfaces (`INestingEngine`, etc.).
- Each task ends with `dotnet build` and a commit.

## Tasks

- [ ] Task 1: Scaffold solution and projects
- [ ] Task 2: Core domain, geometry, contracts
- [ ] Task 3: Nesting engine + tests
- [ ] Task 4: DXF services + tests
- [ ] Task 5: Reports service + tests
- [ ] Task 6: App services (localization, file resolve, project persistence)
- [ ] Task 7: App ViewModel + commands
- [ ] Task 8: App windows (XAML) + startup
- [ ] Task 9: Integration, tests, installer stub
