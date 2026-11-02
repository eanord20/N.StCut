# DXF Export Mode and Dark Theme Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Сделать «Общий DXF» сохраняемым переключателем существующей команды экспорта, вынести обозначения текстуры за лист во всех DXF и восстановить контраст меню/материалов в тёмной теме.

**Architecture:** `MainViewModel.ExportDxfCommand` остаётся единой точкой экспорта и маршрутизирует выполнение по сохраняемому `ExportCombinedDxf`. `DxfExportService` применяет одну общую presentation-функцию к отдельным и объединённым документам, размещая аннотации только во внешней полосе. Theme dictionaries предоставляют явные контрастные brushes/styles вместо несовместимого сочетания MaterialDesign foreground/background.

**Tech Stack:** C# 12, .NET 8, WPF, MVVM, MaterialDesignThemes.Wpf, netDxf, xUnit.

## Global Constraints

- Не изменять и не удалять существующие незакоммиченные изменения пользователя.
- Не коммитить без отдельной просьбы пользователя.
- Исходные DXF-сущности не изменять; экспортировать только clones.
- Один и тот же `NestingResult.Sheets` используется для preview, отчёта и обоих режимов DXF.
- Геометрия деталей и служебные аннотации не должны пересекаться.
- Реализация выполняется TDD: RED → GREEN → refactor для каждого поведения.
- Новые цвета задаются только именованными theme resources.

---

### Task 1: Persisted combined-export mode

**Files:**
- Modify: `src/NStCut.App/Models/AppSettings.cs`
- Modify: `src/NStCut.App/ViewModels/MainViewModel.cs`
- Modify: `src/NStCut.App/MainWindow.xaml`
- Modify: `src/NStCut.App/Services/LocalizationService.cs`
- Test: `src/NStCut.App.Tests/AppSettingsServiceTests.cs`
- Test: `src/NStCut.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Produces: `MainViewModel.ExportCombinedDxf : bool`.
- Persists: `AppSettings.ExportCombinedDxf : bool`.
- Retains: `MainViewModel.ExportDxfCommand : ICommand` as the only export action.

- [ ] **Step 1: Add failing settings round-trip test**

Add `ExportCombinedDxf = true` to the settings fixture and assert the loaded value is true in `SaveThenLoad_RoundTripsSearchAndOutputPaths`.

- [ ] **Step 2: Add failing ViewModel persistence tests**

Add tests that inject `FakeAppSettingsService` with an initial combined mode and verify:

```csharp
Assert.True(vm.ExportCombinedDxf);
vm.ExportCombinedDxf = false;
Assert.False(appSettings.Saved.ExportCombinedDxf);
```

- [ ] **Step 3: Run RED tests**

Run:

```powershell
dotnet test src/NStCut.App.Tests/NStCut.App.Tests.csproj -c Release --filter "AppSettingsServiceTests|ExportCombinedDxf"
```

Expected: compile/assert failures because the persisted property does not exist.

- [ ] **Step 4: Implement the persisted property**

Add the boolean to `AppSettings`; load it in `LoadAppSettings`; include it in `SaveAppSettings`. Add a notifying ViewModel property whose setter saves only when `_loadingSettings == false`.

- [ ] **Step 5: Replace the action menu item with a checkable option**

Replace the `ExportCombinedDxfCommand` menu binding with:

```xml
<MenuItem Header="{Binding Source={x:Static loc:LocalizationService.Instance}, Path=MenuExportCombinedDxf}"
          IsCheckable="True"
          IsChecked="{Binding ExportCombinedDxf, Mode=TwoWay}"
          StaysOpenOnClick="True" />
```

Remove the obsolete public command and private standalone export method after Task 2 routes the main command.

- [ ] **Step 6: Run GREEN tests**

Run the Task 1 filtered command and require zero failures.

---

### Task 2: Route the existing Export DXF button by toggle state

**Files:**
- Modify: `src/NStCut.App/ViewModels/MainViewModel.cs`
- Test: `src/NStCut.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `ExportCombinedDxf : bool` from Task 1.
- Consumes: `IDxfExportService.WriteSheets(...)` and `WriteCombined(...)`.
- Produces: one behavior behind `ExportDxfCommand`.

- [ ] **Step 1: Write two failing routing tests**

Using `FakeDxfExportService`, prepare one `_nestingResult` with two sheets and execute `ExportDxfCommand`:

```csharp
vm.ExportCombinedDxf = true;
vm.ExportDxfCommand.Execute(null);
Assert.True(SpinWait.SpinUntil(() => fakeDxf.LastCombinedPath != null, TimeSpan.FromSeconds(5)));
Assert.Equal(0, fakeDxf.WriteSheetsCallCount);
```

Second test sets false and asserts `WriteSheetsCallCount == 1` and `LastCombinedPath == null`.

- [ ] **Step 2: Run RED routing tests**

Expected: combined mode still invokes the separate-files implementation.

- [ ] **Step 3: Refactor `ExportDxf()` into one dispatcher**

Capture `OutputPath`, sheets, gap and margin before `Task.Run`. When the flag is true, generate `GetCombinedDxfExportPath`, construct localized `DxfExportLabels`, and invoke `WriteCombined`; otherwise retain timestamped per-sheet paths and `WriteSheets`. Keep one `IsBusy` boundary and existing exception dialog.

- [ ] **Step 4: Remove obsolete command state updates**

Delete `ExportCombinedDxfCommand` declaration/initialization and its `RaiseCanExecuteChanged`. Keep `ExportDxfCommand` enabled only when `_nestingResult != null && !IsBusy`.

- [ ] **Step 5: Run App tests**

```powershell
dotnet test src/NStCut.App.Tests/NStCut.App.Tests.csproj -c Release
```

Expected: all App tests pass.

---

### Task 3: Place grain annotations outside every DXF sheet

**Files:**
- Modify: `src/NStCut.Core/Constants/ExportPresentationConstants.cs`
- Modify: `src/NStCut.Dxf/Services/DxfExportService.cs`
- Test: `src/NStCut.Dxf.Tests/DxfServicesTests.cs`

**Interfaces:**
- Retains: `WriteSheet`, `WriteSheets`, and `WriteCombined` signatures.
- Produces: `SheetLabels` and `GrainArrow` entities outside each `SheetBoundary`.

- [ ] **Step 1: Add failing individual-DXF presentation test**

Create a grained sheet, export with `WriteSheet`, load the document, and assert:

```csharp
Assert.Single(doc.Entities.Texts.Where(e => e.Layer.Name == "SheetLabels"));
Assert.Equal(3, doc.Entities.Lines.Count(e => e.Layer.Name == "GrainArrow"));
```

Build the sheet-boundary rectangle and assert every annotation entity lies outside it. For the arrow, all endpoints must have `Y > boundary.MaxY`; for text, insertion `Y > boundary.MaxY`.

- [ ] **Step 2: Strengthen combined-DXF test**

Use horizontal and vertical grained sheets across at least three type groups with increasing and decreasing heights. Assert each sheet has one label, three arrow lines, and no arrow endpoint lies inside any boundary. Assert row boundaries are pairwise non-intersecting.

- [ ] **Step 3: Run RED DXF tests**

Expected: individual exports contain no presentation and combined arrows are currently centered inside sheets.

- [ ] **Step 4: Define an annotation band**

Add named constants for label baseline, arrow baseline, arrow length/head and total annotation-band height. The total must cover both text and arrow plus clearance.

- [ ] **Step 5: Rework presentation coordinates**

Make `WriteDocument` call `AddPresentation` for individual and combined exports. Place label and horizontal/vertical direction arrow above the boundary. The arrow orientation still communicates material grain direction, but all its endpoints remain outside the sheet. For no-grain material write only the external text label.

- [ ] **Step 6: Reserve annotation space in combined layout**

Update the top-cursor layout so each row gap is at least the full annotation-band height plus `SheetSpacing`. The next boundary must begin below the previous boundary and its annotation band.

- [ ] **Step 7: Run GREEN DXF tests**

```powershell
dotnet test src/NStCut.Dxf.Tests/NStCut.Dxf.Tests.csproj -c Release
```

Expected: all DXF tests pass.

---

### Task 4: Restore dark-theme menu and material contrast

**Files:**
- Modify: `src/NStCut.App/AppTheme.xaml`
- Modify: `src/NStCut.App/AppThemeDark.xaml`
- Modify: `src/NStCut.App/MainWindow.xaml`
- Test: `src/NStCut.App.Tests/ThemeResourceTests.cs` (create only if no existing theme-resource test file; otherwise extend it)

**Interfaces:**
- Produces resources with identical keys in both themes:
  - `AppMenuBackgroundBrush`
  - `AppMenuForegroundBrush`
  - `AppInputBackgroundBrush`
  - `AppInputForegroundBrush`
  - `AppInputDropDownBackgroundBrush`

- [ ] **Step 1: Write failing resource completeness tests**

Load each ResourceDictionary and assert all five keys exist, return `SolidColorBrush`, and foreground/background colors differ. Assert light and dark dictionaries contain the same keys.

- [ ] **Step 2: Run RED theme tests**

Expected: keys are missing.

- [ ] **Step 3: Add explicit contrast resources**

Define each key in both dictionaries using the existing palette. Do not put literal colors in `MainWindow.xaml`.

- [ ] **Step 4: Apply menu styles**

Set `Menu.Background/Foreground` and `MenuItem.Background/Foreground` from app resources. Add triggers for highlighted/pressed/disabled states only where the MaterialDesign template honors those properties. Ensure popup submenu surfaces use `AppMenuBackgroundBrush`, not a light system default in dark mode.

- [ ] **Step 5: Apply a dedicated material ComboBox style**

Create `AppMaterialComboBoxStyle` in both theme dictionaries with explicit input foreground/background and an `ItemContainerStyle` for dropdown entries. Apply it only to `ColMaterial` so unrelated controls are not changed. Ensure selected material and dropdown items use `AppInputForegroundBrush` on the corresponding background.

- [ ] **Step 6: Run GREEN theme and App tests**

```powershell
dotnet test src/NStCut.App.Tests/NStCut.App.Tests.csproj -c Release
```

Expected: all tests pass.

- [ ] **Step 7: Manual dark-theme smoke test**

Launch Release and verify all states: top-level menu normal/hover/open, submenu enabled/disabled/checked, selected material, opened material dropdown, and Found/Ambiguous row backgrounds.

---

### Task 5: Final integration and regression verification

**Files:**
- Review only: all files modified in Tasks 1–4.

- [ ] **Step 1: Run scoped tests together**

```powershell
dotnet test src/NStCut.App.Tests/NStCut.App.Tests.csproj -c Release
dotnet test src/NStCut.Dxf.Tests/NStCut.Dxf.Tests.csproj -c Release
```

- [ ] **Step 2: Run the complete suite**

```powershell
dotnet test src/NStCut.sln -c Release
```

Expected: zero failed tests.

- [ ] **Step 3: Build Release**

```powershell
dotnet build src/NStCut.sln -c Release --no-restore
```

Expected: zero errors and zero warnings.

- [ ] **Step 4: Review production DXFs**

Export the same multi-material nesting twice: toggle off and on. Confirm separate-file count equals sheet count when off, exactly one file is created when on, every sheet is present, and all annotations are outside yellow sheet boundaries.

- [ ] **Step 5: Inspect the final diff**

Confirm there are no changes to original DXF geometry, nesting placements, `.gitignore`, or unrelated user files. Do not commit unless the user explicitly requests it.
