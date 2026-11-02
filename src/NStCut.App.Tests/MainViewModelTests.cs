using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NStCut.App.Models;
using NStCut.App.Services;
using NStCut.App.ViewModels;
using NStCut.Core.Constants;
using NStCut.Core.Contracts;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;

namespace NStCut.App.Tests;

public class MainViewModelTests
{
    private static MainViewModel CreateViewModel(
        INestingEngine? nestingEngine = null,
        IDxfGeometryService? dxfGeometry = null,
        IDxfExportService? dxfExport = null,
        IReportService? reportService = null,
        IExportFileNameService? exportFileNameService = null)
        => new(
            nestingEngine ?? new FakeNestingEngine(),
            dxfGeometry ?? new FakeDxfGeometryService(),
            dxfExport ?? new FakeDxfExportService(),
            reportService ?? new FakeReportService(),
            appSettingsService: new FakeAppSettingsService(),
            exportFileNameService: exportFileNameService);

    [Fact]
    public void Constructor_AppliesExplicitZeroMarginAndGapFromSettings()
    {
        var settings = new FakeAppSettingsService(new AppSettings
        {
            SheetMarginMm = 0,
            PartGapMm = 0
        });

        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            appSettingsService: settings);

        Assert.Equal(0, vm.SheetMarginMm);
        Assert.Equal(0, vm.PartGapMm);
    }

    [Theory]
    [InlineData(-1, -2)]
    [InlineData(double.NaN, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, double.NaN)]
    public void Constructor_IgnoresInvalidPersistedMarginAndGap(double margin, double gap)
    {
        var settings = new FakeAppSettingsService(new AppSettings
        {
            SheetMarginMm = margin,
            PartGapMm = gap
        });

        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            appSettingsService: settings);

        Assert.Equal(DefaultNestingOptions.Value.SheetMarginMm, vm.SheetMarginMm);
        Assert.Equal(DefaultNestingOptions.Value.PartGapMm, vm.PartGapMm);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SheetMarginMm_InvalidDirectAssignmentNormalizesAndSavesZero(double value)
    {
        var settings = new FakeAppSettingsService();
        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            appSettingsService: settings);

        vm.SheetMarginMm = value;

        Assert.Equal(0, vm.SheetMarginMm);
        Assert.Equal(0, settings.Saved.SheetMarginMm);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void PartGapMm_InvalidDirectAssignmentNormalizesAndSavesZero(double value)
    {
        var settings = new FakeAppSettingsService();
        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            appSettingsService: settings);

        vm.PartGapMm = value;

        Assert.Equal(0, vm.PartGapMm);
        Assert.Equal(0, settings.Saved.PartGapMm);
    }

    [Theory]
    [InlineData(DxfGeometryQuality.Exact, false, false)]
    [InlineData(DxfGeometryQuality.Partial, true, false)]
    [InlineData(DxfGeometryQuality.BoundsOnly, true, false)]
    [InlineData(DxfGeometryQuality.Invalid, false, true)]
    public void GeometryQuality_ExposesWarningAndErrorState(DxfGeometryQuality quality, bool warning, bool error)
    {
        var row = new PartRow { GeometryQuality = quality };

        Assert.Equal(warning, row.HasGeometryWarning);
        Assert.Equal(error, row.HasGeometryError);
    }

    [Fact]
    public void DuplicateNumbers_AreFlagged_WhenTwoRowsShareNumber()
    {
        var vm = CreateViewModel();
        vm.Rows.Add(new PartRow { Number = "2835.01.008", Title = "A" });
        vm.Rows.Add(new PartRow { Number = "2835.01.008", Title = "B" });
        vm.Rows.Add(new PartRow { Number = "111", Title = "C" });

        Assert.True(vm.Rows[0].IsDuplicateNumber);
        Assert.True(vm.Rows[1].IsDuplicateNumber);
        Assert.False(vm.Rows[2].IsDuplicateNumber);
    }

    [Fact]
    public void DuplicateFlag_Clears_WhenNumberIsEdited()
    {
        var vm = CreateViewModel();
        var row1 = new PartRow { Number = "111", Title = "A" };
        var row2 = new PartRow { Number = "111", Title = "B" };
        vm.Rows.Add(row1);
        vm.Rows.Add(row2);
        Assert.True(row1.IsDuplicateNumber);

        row2.Number = "222";

        Assert.False(row1.IsDuplicateNumber);
        Assert.False(row2.IsDuplicateNumber);
    }

    [Fact]
    public void SectionNumbers_Recompute_WhenMaterialIdChangedViaBinding()
    {
        var vm = CreateViewModel();
        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel" });
        vm.Materials.Add(new MaterialRow { Id = "m2", Name = "Wood" });

        var row1 = new PartRow { Number = "1", MaterialId = "m1" };
        var row2 = new PartRow { Number = "2", MaterialId = "m1" };
        vm.Rows.Add(row1);
        vm.Rows.Add(row2);
        Assert.Equal(1, row1.RowNumberInSection);
        Assert.Equal(2, row2.RowNumberInSection);

        // Reassign row1 directly (simulates the row's Material ComboBox binding), not via MoveRowToMaterial.
        row1.MaterialId = "m2";

        Assert.Equal(1, row1.RowNumberInSection);
        Assert.Equal(1, row2.RowNumberInSection);
    }

    [Fact]
    public void SetActiveMaterialCommand_UpdatesActivePasteMaterialId()
    {
        var vm = CreateViewModel();
        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel" });

        vm.SetActiveMaterialCommand.Execute("m1");

        Assert.Equal("m1", vm.ActivePasteMaterialId);
    }

    [Fact]
    public void CopyDxfPathCommand_CanExecute_WhenRowHasDxfPath()
    {
        var vm = CreateViewModel();
        var rowWithPath = new PartRow { DxfPath = "C:\\Parts\\P1.dxf" };
        var rowWithoutPath = new PartRow();

        Assert.True(vm.CopyDxfPathCommand.CanExecute(rowWithPath));
        Assert.False(vm.CopyDxfPathCommand.CanExecute(rowWithoutPath));
    }

    [Fact]
    public void AssignDroppedDxfToRow_SetsManuallySelectedAndBounds()
    {
        var vm = CreateViewModel();
        var row = new PartRow { Number = "P1" };
        vm.Rows.Add(row);

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            vm.AssignDroppedDxfToRow(row, path);

            Assert.Equal(path, row.DxfPath);
            Assert.Equal(PartStatus.ManuallySelected, row.Status);
            Assert.True(row.IsManuallyResolved);
            Assert.Equal(new Rect(0, 0, 10, 10), row.Bounds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AssignDroppedDxfToRow_MissingFile_SetsNotFoundAndKeepsPath()
    {
        var vm = CreateViewModel();
        var row = new PartRow { Number = "P1" };
        vm.Rows.Add(row);

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");

        vm.AssignDroppedDxfToRow(row, path);

        Assert.Equal(path, row.DxfPath);
        Assert.Equal(PartStatus.NotFound, row.Status);
        Assert.False(row.IsManuallyResolved);
        Assert.Equal(default(Rect), row.Bounds);
    }

    [Fact]
    public void AssignDroppedDxfToRow_BadGeometry_SetsNotFoundAndKeepsPath()
    {
        var vm = CreateViewModel(
            new FakeNestingEngine(),
            new ThrowingDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService());
        var row = new PartRow { Number = "P1" };
        vm.Rows.Add(row);

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            vm.AssignDroppedDxfToRow(row, path);

            Assert.Equal(path, row.DxfPath);
            Assert.Equal(PartStatus.NotFound, row.Status);
            Assert.False(row.IsManuallyResolved);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AssignDroppedDxfToRow_WithExistingPath_IsIgnoredWithoutUndo()
    {
        var vm = CreateViewModel();
        var row = new PartRow
        {
            Number = "P1",
            DxfPath = @"C:\Parts\Existing.dxf",
            Status = PartStatus.ManuallySelected,
            IsManuallyResolved = true,
            Bounds = new Rect(1, 2, 3, 4)
        };
        vm.Rows.Add(row);
        var canUndoBefore = vm.UndoCommand.CanExecute(null);

        vm.AssignDroppedDxfToRow(row, @"C:\Parts\Replacement.dxf");

        Assert.Equal(@"C:\Parts\Existing.dxf", row.DxfPath);
        Assert.Equal(PartStatus.ManuallySelected, row.Status);
        Assert.True(row.IsManuallyResolved);
        Assert.Equal(new Rect(1, 2, 3, 4), row.Bounds);
        Assert.Equal(canUndoBefore, vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void AssignDroppedDxfToRow_CanUndo()
    {
        var vm = CreateViewModel();
        var row = new PartRow { Number = "P1" };
        vm.Rows.Add(row);

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            vm.AssignDroppedDxfToRow(row, path);
            Assert.Equal(path, vm.Rows[0].DxfPath);

            vm.UndoCommand.Execute(null);

            Assert.Empty(vm.Rows[0].DxfPath);
            Assert.Equal(PartStatus.Pending, vm.Rows[0].Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddDroppedDxfFiles_CanUndo()
    {
        var vm = CreateViewModel();

        vm.AddDroppedDxfFiles(new[] { @"C:\A\\Part.dxf" });

        Assert.Single(vm.Rows);
        vm.UndoCommand.Execute(null);

        Assert.Empty(vm.Rows);
    }

    [Fact]
    public void AddDroppedDxfFiles_CreatesRowsFromDxfOnlyAndRespectsActiveMaterial()
    {
        var vm = CreateViewModel();
        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel" });
        vm.ActivePasteMaterialId = "m1";

        var paths = new[]
        {
            @"C:\Folder\\2835.01.008 Cover.dxf",
            @"C:\Folder\\Readme.txt",
            @"C:\Folder\\Side.DXF"
        };

        vm.AddDroppedDxfFiles(paths);

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("2835.01.008", vm.Rows[0].Number);
        Assert.Equal("Cover", vm.Rows[0].Title);
        Assert.Equal("m1", vm.Rows[0].MaterialId);
        Assert.Equal(1, vm.Rows[0].Quantity);
        Assert.Equal(string.Empty, vm.Rows[1].Number);
        Assert.Equal("Side", vm.Rows[1].Title);
    }

    [Fact]
    public void AddDroppedDxfFiles_PushesSingleUndoSnapshot()
    {
        var vm = CreateViewModel();

        vm.AddDroppedDxfFiles(new[] { @"C:\A\\Part.dxf" });

        Assert.Single(vm.Rows);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void AddDroppedDxfFiles_ValidFile_RunsAssignFlowAndSetsManuallySelected()
    {
        var vm = CreateViewModel();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            vm.AddDroppedDxfFiles(new[] { path });

            var row = Assert.Single(vm.Rows);
            Assert.Equal(path, row.DxfPath);
            Assert.Equal(PartStatus.ManuallySelected, row.Status);
            Assert.True(row.IsManuallyResolved);
            Assert.Equal(new Rect(0, 0, 10, 10), row.Bounds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddDroppedDxfFiles_MissingOrBadFile_KeepsPathAndSetsNotFound()
    {
        var vm = CreateViewModel(
            new FakeNestingEngine(),
            new ThrowingDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService());

        var existingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(existingPath, string.Empty);
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        try
        {
            vm.AddDroppedDxfFiles(new[] { existingPath, missingPath });

            Assert.Equal(2, vm.Rows.Count);
            Assert.Equal(existingPath, vm.Rows[0].DxfPath);
            Assert.Equal(PartStatus.NotFound, vm.Rows[0].Status);
            Assert.False(vm.Rows[0].IsManuallyResolved);
            Assert.Equal(default(Rect), vm.Rows[0].Bounds);
            Assert.Equal(missingPath, vm.Rows[1].DxfPath);
            Assert.Equal(PartStatus.NotFound, vm.Rows[1].Status);
            Assert.False(vm.Rows[1].IsManuallyResolved);
            Assert.Equal(default(Rect), vm.Rows[1].Bounds);
        }
        finally
        {
            File.Delete(existingPath);
        }
    }

    [Fact]
    public void AddDroppedDxfFiles_ValidFile_CanUndo()
    {
        var vm = CreateViewModel();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            vm.AddDroppedDxfFiles(new[] { path });
            Assert.Single(vm.Rows);

            vm.UndoCommand.Execute(null);

            Assert.Empty(vm.Rows);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateRowBounds_Exact_SetsBoundsAndNoWarning()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            var vm = CreateViewModel(dxfGeometry: new ConfigurableDxfGeometryService(_ => new DxfContour
            {
                Bounds = new Rect(0, 0, 15, 25),
                Quality = DxfGeometryQuality.Exact,
                Diagnostics = new List<DxfGeometryDiagnostic>()
            }));
            var row = new PartRow { Number = "P1" };
            vm.Rows.Add(row);

            vm.AssignDroppedDxfToRow(row, path);

            Assert.Equal(new Rect(0, 0, 15, 25), row.Bounds);
            Assert.Equal(DxfGeometryQuality.Exact, row.GeometryQuality);
            Assert.Empty(row.GeometryDiagnostics);
            Assert.False(row.HasGeometryWarning);
            Assert.False(row.HasGeometryError);
            Assert.Equal(PartStatus.ManuallySelected, row.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateRowBounds_BoundsOnly_SetsBoundsAndWarning()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        var previousCulture = LocalizationService.Instance.Culture;
        LocalizationService.Instance.Culture = "en";
        try
        {
            var vm = CreateViewModel(dxfGeometry: new ConfigurableDxfGeometryService(_ => new DxfContour
            {
                Bounds = new Rect(0, 0, 12, 22),
                Quality = DxfGeometryQuality.BoundsOnly,
                Diagnostics = new List<DxfGeometryDiagnostic> { new(DxfGeometryDiagnosticCode.NoClosedContour) }
            }));
            var row = new PartRow { Number = "P1" };
            vm.Rows.Add(row);

            vm.AssignDroppedDxfToRow(row, path);

            Assert.Equal(new Rect(0, 0, 12, 22), row.Bounds);
            Assert.Equal(DxfGeometryQuality.BoundsOnly, row.GeometryQuality);
            Assert.Single(row.GeometryDiagnostics);
            Assert.Contains(DxfGeometryDiagnosticCode.NoClosedContour, row.GeometryDiagnostics);
            Assert.True(row.HasGeometryWarning);
            Assert.False(row.HasGeometryError);
            Assert.False(string.IsNullOrWhiteSpace(row.GeometryDiagnosticText));
            Assert.Contains("bounding box", row.GeometryDiagnosticText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(PartStatus.ManuallySelected, row.Status);
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateRowBounds_Invalid_KeepsPathAndSetsError()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        var previousCulture = LocalizationService.Instance.Culture;
        LocalizationService.Instance.Culture = "en";
        try
        {
            var vm = CreateViewModel(dxfGeometry: new ConfigurableDxfGeometryService(_ => new DxfContour
            {
                Bounds = new Rect(0, 0, 12, 22),
                Quality = DxfGeometryQuality.Invalid,
                Diagnostics = new List<DxfGeometryDiagnostic> { new(DxfGeometryDiagnosticCode.NoUsableBounds) }
            }));
            var row = new PartRow { Number = "P1" };
            vm.Rows.Add(row);

            vm.AssignDroppedDxfToRow(row, path);

            Assert.Equal(default(Rect), row.Bounds);
            Assert.Equal(DxfGeometryQuality.Invalid, row.GeometryQuality);
            Assert.Single(row.GeometryDiagnostics);
            Assert.Contains(DxfGeometryDiagnosticCode.NoUsableBounds, row.GeometryDiagnostics);
            Assert.False(row.HasGeometryWarning);
            Assert.True(row.HasGeometryError);
            Assert.False(string.IsNullOrWhiteSpace(row.GeometryDiagnosticText));
            Assert.Equal(path, row.DxfPath);
            Assert.Equal(PartStatus.ManuallySelected, row.Status);
            Assert.True(row.IsManuallyResolved);
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
            File.Delete(path);
        }
    }

    [Fact]
    public void FindFiles_FoundWithInvalidGeometry_PreservesFoundStatusAndClearsBounds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, "P1.dxf");
        File.WriteAllText(path, string.Empty);
        var previousCulture = LocalizationService.Instance.Culture;
        LocalizationService.Instance.Culture = "en";
        try
        {
            var vm = CreateViewModel(dxfGeometry: new ConfigurableDxfGeometryService(_ => new DxfContour
            {
                Bounds = new Rect(0, 0, 12, 22),
                Quality = DxfGeometryQuality.Invalid,
                Diagnostics = new List<DxfGeometryDiagnostic> { new(DxfGeometryDiagnosticCode.NoUsableBounds) }
            }));
            vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel" });
            vm.SearchPath = tempDir;
            var row = new PartRow { Number = "P1", MaterialId = "m1" };
            vm.Rows.Add(row);

            vm.FindFilesCommand.Execute(null);

            Assert.Equal(PartStatus.Found, row.Status);
            Assert.Equal(path, row.DxfPath);
            Assert.Equal(default(Rect), row.Bounds);
            Assert.Equal(DxfGeometryQuality.Invalid, row.GeometryQuality);
            Assert.True(row.HasGeometryError);
            Assert.False(row.HasGeometryWarning);
            Assert.Single(row.GeometryDiagnostics);
            Assert.Contains(DxfGeometryDiagnosticCode.NoUsableBounds, row.GeometryDiagnostics);
            Assert.False(row.IsManuallyResolved);
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MoveRowToMaterial_StillWorks_ForInternalDrag()
    {
        var vm = CreateViewModel();
        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel" });
        vm.Materials.Add(new MaterialRow { Id = "m2", Name = "Wood" });
        var row = new PartRow { Number = "1", MaterialId = "m1" };
        vm.Rows.Add(row);

        vm.MoveRowToMaterial(row, "m2");

        Assert.Equal("m2", row.MaterialId);
    }

    [Theory]
    [InlineData("rows")]
    [InlineData("quantity")]
    [InlineData("number")]
    [InlineData("title")]
    [InlineData("materialId")]
    [InlineData("dxfPath")]
    [InlineData("grainMode")]
    [InlineData("isEnabled")]
    [InlineData("bounds")]
    [InlineData("material")]
    [InlineData("sheetMargin")]
    [InlineData("partGap")]
    [InlineData("maxPasses")]
    [InlineData("noImprovement")]
    [InlineData("acceptableUnplaced")]
    [InlineData("acceptableSheets")]
    public void ExportCommands_AreDisabled_WhenNestingInputChanges(string mutation)
    {
        var vm = CreateViewModel();
        var material = new MaterialRow { Id = "m1", Name = "Steel", Length = 2000, Width = 1000 };
        var row = new PartRow { Number = "P1", MaterialId = material.Id, Quantity = 1 };
        vm.Materials.Add(material);
        vm.Rows.Add(row);
        SetNestingResult(vm);
        Assert.True(vm.ExportDxfCommand.CanExecute(null));
        Assert.True(vm.ExportReportCommand.CanExecute(null));

        switch (mutation)
        {
            case "rows":
                vm.Rows.Add(new PartRow { Number = "P2", MaterialId = material.Id });
                break;
            case "quantity": row.Quantity++; break;
            case "number": row.Number = "P1-new"; break;
            case "title": row.Title = "Part"; break;
            case "materialId": row.MaterialId = "m2"; break;
            case "dxfPath": row.DxfPath = "P1.dxf"; break;
            case "grainMode": row.GrainMode = GrainMode.Allow90; break;
            case "isEnabled": row.IsEnabled = false; break;
            case "bounds": row.Bounds = new Rect(0, 0, 10, 10); break;
            case "material": material.Length++; break;
            case "sheetMargin": vm.SheetMarginMm++; break;
            case "partGap": vm.PartGapMm++; break;
            case "maxPasses": vm.MaxSearchPasses++; break;
            case "noImprovement": vm.NoImprovementStopCount++; break;
            case "acceptableUnplaced": vm.AcceptableMaxUnplaced++; break;
            case "acceptableSheets": vm.AcceptableMaxSheets++; break;
        }

        Assert.Null(GetNestingResult(vm));
        Assert.False(vm.ExportDxfCommand.CanExecute(null));
        Assert.False(vm.ExportReportCommand.CanExecute(null));
    }

    [Fact]
    public void ExportCommands_RemainEnabled_WhenDerivedRowStateChanges()
    {
        var vm = CreateViewModel();
        var row = new PartRow();
        vm.Rows.Add(row);
        SetNestingResult(vm);

        row.RowNumberInSection++;
        row.IsDuplicateNumber = true;
        row.OversizedEvaluation = SheetFitResult.NotEvaluated();

        Assert.NotNull(GetNestingResult(vm));
        Assert.True(vm.ExportDxfCommand.CanExecute(null));
        Assert.True(vm.ExportReportCommand.CanExecute(null));
    }

    private static INestingResultManager GetNestingResultManager(MainViewModel vm)
        => (INestingResultManager)typeof(MainViewModel)
            .GetField("_nestingResultManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(vm)!;

    private static NestingResult? GetNestingResult(MainViewModel vm)
        => GetNestingResultManager(vm).Current;

    private static void SetNestingResult(MainViewModel vm)
    {
        var result = new NestingResult();
        result.Sheets.Add(new SheetInstance
        {
            Material = new SheetMaterial { Name = "Steel", Length = 2000, Width = 1000 },
            Index = 1
        });
        GetNestingResultManager(vm).Set(result);
    }

    [Fact]
    public void ExportDxf_UsesSingleBatchTimestamp_ForAllSheets()
    {
        var fakeExport = new FakeExportFileNameService();
        var fakeDxf = new FakeDxfExportService();
        var vm = CreateViewModel(new FakeNestingEngine(), new FakeDxfGeometryService(), fakeDxf, new FakeReportService(),
            exportFileNameService: fakeExport);

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.OutputPath = tempDir;
            var result = new NestingResult();
            result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Steel", Length = 2000, Width = 1000 }, Index = 1 });
            result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Alum", Length = 1500, Width = 750 }, Index = 2 });

            GetNestingResultManager(vm).Set(result);

            vm.ExportDxfCommand.Execute(null);

            var completed = SpinWait.SpinUntil(() => fakeDxf.WrittenPaths.Count == 2, TimeSpan.FromSeconds(5));
            Assert.True(completed, "Expected both sheets to be exported");
            Assert.Equal(1, fakeExport.TimestampRequestCount);
            Assert.Single(fakeExport.DxfTimestamps.Distinct());
            Assert.Equal(2, fakeExport.DxfPaths.Count);
            Assert.All(fakeExport.DxfPaths, p => Assert.Contains("20250415", p));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ExportDxfCommand_CanExecute_FalseWhileExporting()
    {
        var fakeDxf = new FakeDxfExportService();
        fakeDxf.Gate.Reset();

        var fakeExport = new FakeExportFileNameService();
        var vm = CreateViewModel(new FakeNestingEngine(), new FakeDxfGeometryService(), fakeDxf, new FakeReportService(),
            exportFileNameService: fakeExport);

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.OutputPath = tempDir;
            var result = new NestingResult();
            result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Steel", Length = 2000, Width = 1000 }, Index = 1 });
            result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Alum", Length = 1500, Width = 750 }, Index = 2 });

            GetNestingResultManager(vm).Set(result);

            Assert.True(vm.ExportDxfCommand.CanExecute(null));

            vm.ExportDxfCommand.Execute(null);

            var entered = SpinWait.SpinUntil(() => fakeDxf.EntryCount > 0, TimeSpan.FromSeconds(5));
            Assert.True(entered, "Expected WriteSheet to be entered");
            Assert.False(vm.ExportDxfCommand.CanExecute(null));
            Assert.False(vm.ExportReportCommand.CanExecute(null));

            fakeDxf.Gate.Set();
            var completed = SpinWait.SpinUntil(() => fakeDxf.WrittenPaths.Count == 2, TimeSpan.FromSeconds(5));
            Assert.True(completed, "Expected both sheets to finish");
            var reenabled = SpinWait.SpinUntil(() => vm.ExportDxfCommand.CanExecute(null), TimeSpan.FromSeconds(5));
            Assert.True(reenabled, "Expected commands to re-enable after export");
            Assert.True(vm.ExportReportCommand.CanExecute(null));
        }
        finally
        {
            fakeDxf.Gate.Set();
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ExportDxfCommand_CombinedMode_CallsWriteCombinedNotWriteSheets()
    {
        var fakeExport = new FakeExportFileNameService();
        var fakeDxf = new FakeDxfExportService();
        var vm = CreateViewModel(new FakeNestingEngine(), new FakeDxfGeometryService(), fakeDxf, new FakeReportService(),
            exportFileNameService: fakeExport);

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.OutputPath = tempDir;
            var result = new NestingResult();
            result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Steel", Length = 2000, Width = 1000 }, Index = 1 });
            result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Alum", Length = 1500, Width = 750 }, Index = 2 });

            GetNestingResultManager(vm).Set(result);

            vm.ExportCombinedDxf = true;
            vm.ExportDxfCommand.Execute(null);

            var completed = SpinWait.SpinUntil(() => fakeDxf.LastCombinedPath != null, TimeSpan.FromSeconds(5));
            Assert.True(completed, "Expected WriteCombined to be called");
            Assert.Equal(0, fakeDxf.WriteSheetsCallCount);
            Assert.Equal(Path.Combine(tempDir, "Combined_20250415.dxf"), fakeDxf.LastCombinedPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ExportDxfCommand_SeparateMode_CallsWriteSheetsNotWriteCombined()
    {
        var fakeExport = new FakeExportFileNameService();
        var fakeDxf = new FakeDxfExportService();
        var vm = CreateViewModel(new FakeNestingEngine(), new FakeDxfGeometryService(), fakeDxf, new FakeReportService(),
            exportFileNameService: fakeExport);

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.OutputPath = tempDir;
            var result = new NestingResult();
            result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Steel", Length = 2000, Width = 1000 }, Index = 1 });
            result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Alum", Length = 1500, Width = 750 }, Index = 2 });

            GetNestingResultManager(vm).Set(result);

            vm.ExportCombinedDxf = false;
            vm.ExportDxfCommand.Execute(null);

            var completed = SpinWait.SpinUntil(() => fakeDxf.WrittenPaths.Count == 2, TimeSpan.FromSeconds(5));
            Assert.True(completed, "Expected both sheets to be exported");
            Assert.Equal(1, fakeDxf.WriteSheetsCallCount);
            Assert.Null(fakeDxf.LastCombinedPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ExportReportCommand_CanExecute_RespectsIsBusyGuard()
    {
        var vm = CreateViewModel();
        var result = new NestingResult();
        result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Steel", Length = 2000, Width = 1000 }, Index = 1 });

        GetNestingResultManager(vm).Set(result);

        Assert.True(vm.ExportReportCommand.CanExecute(null));

        vm.IsBusy = true;

        Assert.False(vm.ExportDxfCommand.CanExecute(null));
        Assert.False(vm.ExportReportCommand.CanExecute(null));

        vm.IsBusy = false;

        Assert.True(vm.ExportDxfCommand.CanExecute(null));
        Assert.True(vm.ExportReportCommand.CanExecute(null));
    }

    [Fact]
    public void ExportReport_SetsLastReportPath_ToGeneratedReportPath()
    {
        var fakeExport = new FakeExportFileNameService();
        var fakeReport = new FakeReportService();
        var vm = CreateViewModel(new FakeNestingEngine(), new FakeDxfGeometryService(), new FakeDxfExportService(), fakeReport,
            exportFileNameService: fakeExport);

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            vm.OutputPath = tempDir;
            var result = new NestingResult();
            result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Steel", Length = 2000, Width = 1000 }, Index = 1 });

            var manager = GetNestingResultManager(vm);
            manager.Set(result);

            vm.ExportReportCommand.Execute(null);

            var completed = SpinWait.SpinUntil(() => manager.LastReportPath is not null, TimeSpan.FromSeconds(5));
            Assert.True(completed, "Expected report generation to complete");

            Assert.NotNull(fakeReport.LastOutputPath);
            Assert.Equal(Path.Combine(tempDir, "Report.docx"), fakeReport.LastOutputPath);
            Assert.Equal(fakeReport.LastOutputPath, manager.LastReportPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData("en", "Material")]
    [InlineData("ua", "Матеріал")]
    public void Localization_ProvidesMaterialLabel(string culture, string expected)
    {
        var previousCulture = LocalizationService.Instance.Culture;
        try
        {
            LocalizationService.Instance.Culture = culture;
            Assert.Equal(expected, LocalizationService.Instance.MaterialLabel);
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
        }
    }

    [Fact]
    public void ExportDxfCommand_CombinedMode_PassesPathSheetsAndLabels()
    {
        var previousCulture = LocalizationService.Instance.Culture;
        LocalizationService.Instance.Culture = "en";
        try
        {
            var fakeDxf = new FakeDxfExportService();
            var fakeExport = new FakeExportFileNameService();
            var vm = CreateViewModel(new FakeNestingEngine(), new FakeDxfGeometryService(), fakeDxf, new FakeReportService(),
                exportFileNameService: fakeExport);

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                vm.OutputPath = tempDir;
                var result = new NestingResult();
                var firstSheet = new SheetInstance { Material = new SheetMaterial { Name = "Steel", Length = 2000, Width = 1000 }, Index = 1 };
                var secondSheet = new SheetInstance { Material = new SheetMaterial { Name = "Aluminium", Length = 1500, Width = 750 }, Index = 2 };
                result.Sheets.Add(firstSheet);
                result.Sheets.Add(secondSheet);

                GetNestingResultManager(vm).Set(result);

                vm.ExportCombinedDxf = true;
                vm.ExportDxfCommand.Execute(null);

                var completed = SpinWait.SpinUntil(() => fakeDxf.LastCombinedPath != null, TimeSpan.FromSeconds(5));
                Assert.True(completed, "Expected WriteCombined to be called");
                Assert.Equal(Path.Combine(tempDir, "Combined_20250415.dxf"), fakeDxf.LastCombinedPath);
                Assert.NotNull(fakeDxf.LastCombinedSheets);
                Assert.Equal(2, fakeDxf.LastCombinedSheets!.Count);
                Assert.Same(result.Sheets[0], fakeDxf.LastCombinedSheets[0]);
                Assert.Same(result.Sheets[1], fakeDxf.LastCombinedSheets[1]);
                Assert.NotNull(fakeDxf.LastCombinedLabels);
                Assert.Equal("Material", fakeDxf.LastCombinedLabels!.Material);
                Assert.Equal("Sheet", fakeDxf.LastCombinedLabels.Sheet);
                Assert.Equal("No grain", fakeDxf.LastCombinedLabels.NoGrain);
                Assert.Equal("Horizontal", fakeDxf.LastCombinedLabels.Horizontal);
                Assert.Equal("Vertical", fakeDxf.LastCombinedLabels.Vertical);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
        }
    }

    [Fact]
    public void PerformNesting_InvalidGeometry_ReturnsValueUpdateAndUnplacedCopiesWithoutCallingEngineWithPart()
    {
        var engine = new RecordingNestingEngine();
        var vm = CreateViewModel(
            engine,
            new ThrowingDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService());

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            var part = new Part
            {
                Number = "P1",
                Quantity = 3,
                DxfPath = path,
                Bounds = new Rect(0, 0, 100, 100),
                Status = PartStatus.Pending
            };

            var work = vm.PerformNesting(
                new NestingOptions(),
                new Dictionary<string, SheetMaterial>(),
                new[] { new MainViewModel.NestingInput(7, part) },
                null,
                default);

            var update = Assert.Single(work.Updates);
            Assert.Equal(7, update.RowIndex);
            Assert.Equal(default, update.Bounds);
            Assert.Equal(PartStatus.NotFound, update.Status);
            Assert.Empty(engine.ReceivedParts);
            Assert.Equal(3, work.Result.Unplaced.Count);
            Assert.All(work.Result.Unplaced, copy =>
            {
                Assert.Equal(1, copy.Quantity);
                Assert.Equal(default, copy.Bounds);
                Assert.Equal(PartStatus.NotFound, copy.Status);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(DxfGeometryQuality.Partial, DxfGeometryDiagnosticCode.UnsupportedEntity)]
    [InlineData(DxfGeometryQuality.BoundsOnly, DxfGeometryDiagnosticCode.NoClosedContour)]
    public async Task NestAsync_UsableSimplifiedGeometry_ReachesEngineAndPreservesDiagnostics(
        DxfGeometryQuality quality,
        DxfGeometryDiagnosticCode diagnostic)
    {
        var engine = new RecordingNestingEngine();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            var vm = CreateViewModel(
                engine,
                new ConfigurableDxfGeometryService(_ => new DxfContour
                {
                    Bounds = new Rect(0, 0, 10, 20),
                    Quality = quality,
                    Diagnostics = new List<DxfGeometryDiagnostic> { new(diagnostic) }
                }),
                new FakeDxfExportService(),
                new FakeReportService());
            vm.Rows.Add(new PartRow
            {
                Number = "P1",
                DxfPath = path,
                Quantity = 1,
                Status = PartStatus.Found,
                IsEnabled = true
            });

            await vm.NestAsync();

            Assert.Single(engine.ReceivedParts);
            Assert.Equal(new Rect(0, 0, 10, 20), vm.Rows[0].Bounds);
            Assert.Equal(quality, vm.Rows[0].GeometryQuality);
            Assert.Equal(new[] { diagnostic }, vm.Rows[0].GeometryDiagnostics);
            Assert.NotEmpty(vm.Rows[0].GeometryDiagnosticText);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NestAsync_InvalidGeometry_ExcludesPartAndPreservesQuantityAndDiagnostics()
    {
        var engine = new RecordingNestingEngine();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            var vm = CreateViewModel(
                engine,
                new ConfigurableDxfGeometryService(_ => new DxfContour
                {
                    Bounds = new Rect(0, 0, 10, 20),
                    Quality = DxfGeometryQuality.Invalid,
                    Diagnostics = new List<DxfGeometryDiagnostic> { new(DxfGeometryDiagnosticCode.NoUsableBounds) }
                }),
                new FakeDxfExportService(),
                new FakeReportService());
            vm.Rows.Add(new PartRow
            {
                Number = "P1",
                DxfPath = path,
                Quantity = 3,
                Status = PartStatus.Found,
                IsEnabled = true
            });

            var result = await vm.NestAsync();

            Assert.Empty(engine.ReceivedParts);
            Assert.Equal(3, result.Unplaced.Count);
            Assert.Equal(PartStatus.Found, vm.Rows[0].Status);
            Assert.All(result.Unplaced, copy => Assert.Equal(PartStatus.Found, copy.Status));
            Assert.Equal(DxfGeometryQuality.Invalid, vm.Rows[0].GeometryQuality);
            Assert.Equal(new[] { DxfGeometryDiagnosticCode.NoUsableBounds }, vm.Rows[0].GeometryDiagnostics);
            Assert.NotEmpty(vm.Rows[0].GeometryDiagnosticText);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PerformNesting_GeometryInvalid_ResolvedFile_PreservesStatusAndCreatesUnplacedCopies()
    {
        var engine = new RecordingNestingEngine();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            var vm = CreateViewModel(
                engine,
                new ConfigurableDxfGeometryService(_ => new DxfContour
                {
                    Bounds = new Rect(0, 0, 10, 20),
                    Quality = DxfGeometryQuality.Invalid,
                    Diagnostics = new List<DxfGeometryDiagnostic> { new(DxfGeometryDiagnosticCode.NoClosedContour) }
                }),
                new FakeDxfExportService(),
                new FakeReportService());
            var part = new Part
            {
                Number = "P1",
                Quantity = 3,
                DxfPath = path,
                Status = PartStatus.ManuallySelected,
                IsManuallyResolved = true
            };

            var work = vm.PerformNesting(
                new NestingOptions(),
                new Dictionary<string, SheetMaterial>(),
                new[] { new MainViewModel.NestingInput(0, part) },
                null,
                default);

            var update = Assert.Single(work.Updates);
            Assert.Equal(0, update.RowIndex);
            Assert.Equal(default, update.Bounds);
            Assert.Equal(PartStatus.ManuallySelected, update.Status);
            Assert.Equal(DxfGeometryQuality.Invalid, update.GeometryQuality);
            Assert.Equal(new[] { DxfGeometryDiagnosticCode.NoClosedContour }, update.GeometryDiagnostics);
            Assert.Empty(engine.ReceivedParts);
            Assert.Equal(3, work.Result.Unplaced.Count);
            Assert.All(work.Result.Unplaced, copy =>
            {
                Assert.Equal(1, copy.Quantity);
                Assert.Equal(default, copy.Bounds);
                Assert.Equal(PartStatus.ManuallySelected, copy.Status);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NestAsync_MixedGeometry_AppendsDistinctSimplifiedRowCount()
    {
        var previousCulture = LocalizationService.Instance.Culture;
        LocalizationService.Instance.Culture = "en";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var qualities = new Dictionary<string, DxfGeometryQuality>
            {
                ["exact.dxf"] = DxfGeometryQuality.Exact,
                ["partial.dxf"] = DxfGeometryQuality.Partial,
                ["bounds.dxf"] = DxfGeometryQuality.BoundsOnly
            };
            var engine = new RecordingNestingEngine();
            var vm = CreateViewModel(
                engine,
                new ConfigurableDxfGeometryService(path => new DxfContour
                {
                    Bounds = new Rect(0, 0, 10, 20),
                    Quality = qualities[Path.GetFileName(path)],
                    Diagnostics = new List<DxfGeometryDiagnostic>()
                }),
                new FakeDxfExportService(),
                new FakeReportService());
            foreach (var fileName in qualities.Keys)
            {
                var path = Path.Combine(tempDir, fileName);
                File.WriteAllText(path, string.Empty);
                vm.Rows.Add(new PartRow
                {
                    Number = fileName,
                    DxfPath = path,
                    Quantity = fileName == "partial.dxf" ? 3 : 1,
                    Status = PartStatus.Found,
                    IsEnabled = true
                });
            }

            await vm.NestAsync();

            Assert.Equal(3, engine.ReceivedParts.Count);
            Assert.Equal(DxfGeometryQuality.Exact, vm.Rows.Single(row => row.Number == "exact.dxf").GeometryQuality);
            Assert.Equal("Sheets: 0, unplaced: 0 (heuristic; optimality not guaranteed) (2 simplified)", vm.StatusText);
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task NestAsync_Success_RaisesExportDxfCanExecuteChangedAndEnablesCommand()
    {
        var vm = CreateViewModel();
        var raised = 0;
        vm.ExportDxfCommand.CanExecuteChanged += (_, _) => raised++;

        Assert.False(vm.ExportDxfCommand.CanExecute(null));

        await vm.NestAsync();

        Assert.True(raised > 0);
        Assert.True(vm.ExportDxfCommand.CanExecute(null));
    }

    [Fact]
    public async Task NestAsync_AllInvalid_AppliesUpdatesClearsBusyAndReportsUnplacedCount()
    {
        var engine = new RecordingNestingEngine();
        var vm = CreateViewModel(
            engine,
            new ThrowingDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService());
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        var row = new PartRow
        {
            Number = "P1",
            Quantity = 2,
            DxfPath = missingPath,
            Bounds = new Rect(0, 0, 100, 100),
            Status = PartStatus.Pending,
            IsEnabled = true
        };
        vm.Rows.Add(row);

        var result = await vm.NestAsync();

        Assert.False(vm.IsBusy);
        Assert.Empty(engine.ReceivedParts);
        Assert.Equal(default, row.Bounds);
        Assert.Equal(PartStatus.NotFound, row.Status);
        Assert.Equal(2, result.Unplaced.Count);
        Assert.Equal("Sheets: 0, unplaced: 2 (heuristic; optimality not guaranteed)", vm.StatusText);
    }

    [Fact]
    public void PerformNesting_InvalidPartWithZeroQuantity_AddsNoUnplacedCopies()
    {
        var vm = CreateViewModel(
            new RecordingNestingEngine(),
            new ThrowingDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService());
        var part = new Part { Quantity = 0, DxfPath = "missing.dxf", Bounds = new Rect(0, 0, 10, 10) };

        var work = vm.PerformNesting(
            new NestingOptions(),
            new Dictionary<string, SheetMaterial>(),
            new[] { new MainViewModel.NestingInput(0, part) },
            null,
            default);

        Assert.Empty(work.Result.Unplaced);
        Assert.Single(work.Updates);
    }

    [Fact]
    public void Oversized_Recomputed_WhenBoundsAndMaterialAssigned()
    {
        var vm = CreateViewModel();
        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel", Length = 20, Width = 18 });
        var row = new PartRow { Number = "P1", MaterialId = "m1", Bounds = new Rect(0, 0, 20, 20) };
        vm.Rows.Add(row);

        vm.SheetMarginMm = 1;
        vm.PartGapMm = 1;

        Assert.True(row.IsOversized);
        Assert.NotNull(row.OversizedEvaluation);
        Assert.False(row.OversizedEvaluation!.Fits);
        Assert.Equal(18, row.OversizedEvaluation.UsableSheetWidth, 6);
        Assert.Equal(16, row.OversizedEvaluation.UsableSheetHeight, 6);
    }

    [Fact]
    public void Oversized_ChangingMarginCanMakeFittingPartOversized()
    {
        var vm = CreateViewModel();
        vm.SheetMarginMm = 0;
        vm.PartGapMm = 0;
        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel", Length = 30, Width = 20 });
        var row = new PartRow { Number = "P1", MaterialId = "m1", Bounds = new Rect(0, 0, 10, 10) };
        vm.Rows.Add(row);

        Assert.False(row.IsOversized);

        vm.SheetMarginMm = 9; // usable 12x2 -> 10x10 no longer fits

        Assert.True(row.IsOversized);
    }

    [Fact]
    public void Oversized_RotationAllowsFit_UntilFixed0BlocksIt()
    {
        var vm = CreateViewModel();
        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel", Length = 25, Width = 18, HasGrain = false });
        var row = new PartRow { Number = "P1", MaterialId = "m1", Bounds = new Rect(0, 0, 10, 20), GrainMode = GrainMode.FollowMaterial };
        vm.Rows.Add(row);
        vm.SheetMarginMm = 1;
        vm.PartGapMm = 1;

        Assert.True(row.OversizedEvaluation!.Fits);

        row.GrainMode = GrainMode.Fixed0;

        Assert.True(row.IsOversized);
    }

    [Fact]
    public async Task Oversized_NestAsync_DoesNotSendToEngine_AddsQuantityCopiesToUnplaced_KeepsStatus()
    {
        var engine = new RecordingNestingEngine();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dxf");
        File.WriteAllText(path, string.Empty);
        try
        {
            var vm = CreateViewModel(
                engine,
                new ConfigurableDxfGeometryService(new Rect(0, 0, 20, 20)),
                new FakeDxfExportService(),
                new FakeReportService());
            vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel", Length = 20, Width = 20 });
            var row = new PartRow { Number = "P1", MaterialId = "m1", DxfPath = path, Quantity = 3, Status = PartStatus.Found };
            vm.Rows.Add(row);
            vm.SheetMarginMm = 1;
            vm.PartGapMm = 1;

            var result = await vm.NestAsync();

            Assert.True(row.IsOversized);
            Assert.Equal(PartStatus.Found, row.Status);
            Assert.Empty(engine.ReceivedParts);
            Assert.Equal(3, result.Unplaced.Count);
            Assert.All(result.Unplaced, p => Assert.Equal(PartStatus.Found, p.Status));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Oversized_SelectedRow_SetsLocalizedStatusText()
    {
        var previousCulture = LocalizationService.Instance.Culture;
        LocalizationService.Instance.Culture = "en";
        try
        {
            var vm = CreateViewModel();
            vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel", Length = 20, Width = 18 });
            var row = new PartRow { Number = "P1", MaterialId = "m1", Bounds = new Rect(0, 0, 20, 20) };
            vm.Rows.Add(row);
            vm.SheetMarginMm = 1;
            vm.PartGapMm = 1;

            vm.SelectedRow = row;

            Assert.Contains("20", vm.StatusText);
            Assert.Contains("18", vm.StatusText);
            Assert.Contains("16", vm.StatusText);
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
        }
    }

    [Fact]
    public void Oversized_SelectingNormalOrNull_ClearsOnlyOversizedStatus()
    {
        var vm = CreateViewModel();
        vm.SheetMarginMm = 0;
        vm.PartGapMm = 0;
        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel", Length = 20, Width = 20 });
        var oversized = new PartRow { MaterialId = "m1", Bounds = new Rect(0, 0, 30, 30) };
        var normal = new PartRow { MaterialId = "m1", Bounds = new Rect(0, 0, 10, 10) };
        vm.Rows.Add(oversized);
        vm.Rows.Add(normal);

        vm.SelectedRow = oversized;
        Assert.NotEmpty(vm.StatusText);

        vm.SelectedRow = normal;
        Assert.Empty(vm.StatusText);

        vm.SelectedRow = oversized;
        Assert.NotEmpty(vm.StatusText);
        vm.SelectedRow = null;
        Assert.Empty(vm.StatusText);
    }

    [Fact]
    public void Oversized_SelectionChange_DoesNotClearUnrelatedOperationStatus()
    {
        var vm = CreateViewModel();
        vm.SheetMarginMm = 0;
        vm.PartGapMm = 0;
        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel", Length = 20, Width = 20 });
        var oversized = new PartRow { MaterialId = "m1", Bounds = new Rect(0, 0, 30, 30) };
        var normal = new PartRow { MaterialId = "m1", Bounds = new Rect(0, 0, 10, 10) };
        vm.Rows.Add(oversized);
        vm.Rows.Add(normal);
        vm.SelectedRow = oversized;
        vm.StatusText = "Operation failed";

        vm.SelectedRow = normal;

        Assert.Equal("Operation failed", vm.StatusText);
    }

    [Fact]
    public void Oversized_MissingMaterialOrBounds_NotEvaluatedAndNotOversized()
    {
        var vm = CreateViewModel();
        vm.SheetMarginMm = 0;
        vm.PartGapMm = 0;

        var rowWithBounds = new PartRow { Number = "P1", Bounds = new Rect(0, 0, 100, 100) };
        vm.Rows.Add(rowWithBounds);
        Assert.False(rowWithBounds.IsOversized);

        vm.Materials.Add(new MaterialRow { Id = "m1", Name = "Steel", Length = 10, Width = 10 });
        rowWithBounds.MaterialId = "m1";
        Assert.True(rowWithBounds.IsOversized);

        var rowNoBounds = new PartRow { Number = "P2", MaterialId = "m1" };
        vm.Rows.Add(rowNoBounds);
        Assert.False(rowNoBounds.IsOversized);
    }

    [Fact]
    public void DeleteRowCommand_RemovesSpecifiedRow_WhenSelectionIsDifferent()
    {
        var vm = CreateViewModel();
        var row1 = new PartRow { Number = "A" };
        var row2 = new PartRow { Number = "B" };
        vm.Rows.Add(row1);
        vm.Rows.Add(row2);
        vm.SelectedRow = row1;

        Assert.True(vm.DeleteRowCommand.CanExecute(row2));
        vm.DeleteRowCommand.Execute(row2);

        Assert.Single(vm.Rows);
        Assert.Same(row1, vm.Rows[0]);
        Assert.Same(row1, vm.SelectedRow);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteRowCommand_RemovesSelectedRow_AndSelectsFirstRemaining()
    {
        var vm = CreateViewModel();
        var row1 = new PartRow { Number = "A" };
        var row2 = new PartRow { Number = "B" };
        vm.Rows.Add(row1);
        vm.Rows.Add(row2);
        vm.SelectedRow = row1;

        vm.DeleteRowCommand.Execute(row1);

        Assert.Single(vm.Rows);
        Assert.Same(row2, vm.Rows[0]);
        Assert.Same(row2, vm.SelectedRow);
    }

    [Fact]
    public void DeleteSelectedRowsCommand_RemovesSelectedRow()
    {
        var vm = CreateViewModel();
        var row1 = new PartRow { Number = "A" };
        var row2 = new PartRow { Number = "B" };
        vm.Rows.Add(row1);
        vm.Rows.Add(row2);
        vm.SelectedRow = row1;

        Assert.True(vm.DeleteSelectedRowsCommand.CanExecute(null));
        vm.DeleteSelectedRowsCommand.Execute(null);

        Assert.Single(vm.Rows);
        Assert.Same(row2, vm.Rows[0]);
        Assert.Same(row2, vm.SelectedRow);
    }

    [Fact]
    public void OpenProject_WithNonEmptyPaths_AppliesProjectPathsAndSavesThem()
    {
        var appSettings = new FakeAppSettingsService();
        var projectService = new FakeProjectFileService
        {
            Data = new ProjectData
            {
                SearchPath = @"C:\project\dxf",
                OutputPath = @"C:\project\out",
                Parts = new List<Part> { new() { Number = "P1" } },
                Materials = new List<SheetMaterial> { new() { Name = "Steel", Length = 1000, Width = 500 } }
            }
        };
        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            projectService: projectService,
            appSettingsService: appSettings);

        vm.OpenProject(@"C:\project.nstcut");

        Assert.Equal(@"C:\project\dxf", vm.SearchPath);
        Assert.Equal(@"C:\project\out", vm.OutputPath);
        Assert.Equal(@"C:\project\dxf", appSettings.Saved.SearchPath);
        Assert.Equal(@"C:\project\out", appSettings.Saved.OutputPath);
        Assert.Single(vm.Rows);
        Assert.Single(vm.Materials);
    }

    [Fact]
    public void OpenProject_WithEmptyPaths_KeepsCurrentGlobalPaths()
    {
        var appSettings = new FakeAppSettingsService();
        var projectService = new FakeProjectFileService
        {
            Data = new ProjectData
            {
                SearchPath = string.Empty,
                OutputPath = string.Empty,
                Parts = new List<Part> { new() { Number = "P1" } },
                Materials = new List<SheetMaterial>()
            }
        };
        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            projectService: projectService,
            appSettingsService: appSettings);
        vm.SearchPath = @"C:\global\dxf";
        vm.OutputPath = @"C:\global\out";

        vm.OpenProject(@"C:\empty.nstcut");

        Assert.Equal(@"C:\global\dxf", vm.SearchPath);
        Assert.Equal(@"C:\global\out", vm.OutputPath);
        Assert.NotEqual(string.Empty, appSettings.Saved.SearchPath);
        Assert.NotEqual(string.Empty, appSettings.Saved.OutputPath);
    }

    [Fact]
    public void SheetMarginMm_Change_SavesAppSettings()
    {
        var appSettings = new FakeAppSettingsService();
        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            appSettingsService: appSettings);

        vm.SheetMarginMm = 9.5;

        Assert.Equal(9.5, appSettings.Saved.SheetMarginMm);
    }

    [Fact]
    public void PartGapMm_Change_SavesAppSettings()
    {
        var appSettings = new FakeAppSettingsService();
        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            appSettingsService: appSettings);

        vm.PartGapMm = 2.5;

        Assert.Equal(2.5, appSettings.Saved.PartGapMm);
    }

    [Fact]
    public void ExportCombinedDxf_LoadedFromAppSettings()
    {
        var appSettings = new FakeAppSettingsService(new AppSettings { ExportCombinedDxf = true });
        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            appSettingsService: appSettings);

        Assert.True(vm.ExportCombinedDxf);
    }

    [Fact]
    public void ExportCombinedDxf_Change_SavesAppSettings()
    {
        var appSettings = new FakeAppSettingsService(new AppSettings { ExportCombinedDxf = true });
        var vm = new MainViewModel(
            new FakeNestingEngine(),
            new FakeDxfGeometryService(),
            new FakeDxfExportService(),
            new FakeReportService(),
            appSettingsService: appSettings);

        vm.ExportCombinedDxf = false;

        Assert.False(appSettings.Saved.ExportCombinedDxf);
    }

    [Fact]
    public void ExportReport_PassesLocalizedReportLabels()
    {
        var previousCulture = LocalizationService.Instance.Culture;
        LocalizationService.Instance.Culture = "en";
        try
        {
            var fakeReport = new FakeReportService();
            var fakeExport = new FakeExportFileNameService();
            var vm = CreateViewModel(new FakeNestingEngine(), new FakeDxfGeometryService(), new FakeDxfExportService(), fakeReport,
                exportFileNameService: fakeExport);

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                vm.OutputPath = tempDir;
                var result = new NestingResult();
                result.Sheets.Add(new SheetInstance { Material = new SheetMaterial { Name = "Steel", Length = 100, Width = 100 }, Index = 1 });

                GetNestingResultManager(vm).Set(result);

                vm.ExportReportCommand.Execute(null);

                var completed = SpinWait.SpinUntil(() => fakeReport.LastLabels is not null, TimeSpan.FromSeconds(5));
                Assert.True(completed, "Expected report generation to complete");

                Assert.Equal("Direction", fakeReport.LastLabels!.DirectionHeader);
                Assert.Equal("No grain", fakeReport.LastLabels.NoGrain);
                Assert.Equal("Horizontal", fakeReport.LastLabels.Horizontal);
                Assert.Equal("Vertical", fakeReport.LastLabels.Vertical);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
        }
    }

    [Fact]
    public void SheetPreviewItemViewModel_GrainMaterial_ProducesArrowGeometry()
    {
        var sheet = new SheetInstance
        {
            Material = new SheetMaterial { Name = "Steel", Length = 100, Width = 50, HasGrain = true, GrainDirection = GrainDirection.Horizontal },
            Index = 1
        };
        var vm = new SheetPreviewItemViewModel(sheet);

        Assert.False(string.IsNullOrEmpty(vm.ArrowGeometry));
        Assert.Equal(3, vm.ArrowGeometry!.Split('M', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void SheetPreviewItemViewModel_NoGrain_DoesNotProduceArrowGeometry()
    {
        var sheet = new SheetInstance
        {
            Material = new SheetMaterial { Name = "Steel", Length = 100, Width = 50, HasGrain = false },
            Index = 1
        };
        var vm = new SheetPreviewItemViewModel(sheet);

        Assert.True(string.IsNullOrEmpty(vm.ArrowGeometry));
    }

    [Fact]
    public async Task Profile_NestAsync_UI_Overhead_WithManyRows()
    {
        var vm = CreateViewModel(
            nestingEngine: new FakeNestingEngine(),
            dxfGeometry: new FakeDxfGeometryService());
        vm.Materials.Add(new MaterialRow { Name = "Steel", Length = 2800, Width = 2070 });
        var materialId = vm.Materials[0].Id;

        for (var i = 0; i < 500; i++)
        {
            vm.Rows.Add(new PartRow
            {
                Number = $"P{i}",
                DxfPath = $"part{i}.dxf",
                MaterialId = materialId,
                Quantity = 1,
                IsEnabled = true
            });
        }

        var sw = Stopwatch.StartNew();
        var result = await vm.NestAsync();
        sw.Stop();

        Console.WriteLine($"MainViewModel.NestAsync with {vm.Rows.Count} rows: {sw.Elapsed.TotalMilliseconds:F1} ms");
        Assert.NotNull(result);
    }
}

internal sealed class FakeAppSettingsService : IAppSettingsService
{
    public FakeAppSettingsService(AppSettings? initial = null)
    {
        if (initial != null)
            Saved = initial;
    }

    public AppSettings Saved { get; private set; } = new()
    {
        SheetMarginMm = DefaultNestingOptions.Value.SheetMarginMm,
        PartGapMm = DefaultNestingOptions.Value.PartGapMm,
        Culture = "en",
        Theme = nameof(AppTheme.Light)
    };

    public AppSettings Load() => new()
    {
        SearchPath = Saved.SearchPath,
        OutputPath = Saved.OutputPath,
        SheetMarginMm = Saved.SheetMarginMm,
        PartGapMm = Saved.PartGapMm,
        Culture = Saved.Culture,
        Theme = Saved.Theme,
        PartsGridColumnWidths = new Dictionary<string, double>(Saved.PartsGridColumnWidths),
        ExportCombinedDxf = Saved.ExportCombinedDxf
    };

    public void Save(AppSettings settings)
    {
        Saved = settings;
    }
}

internal sealed class FakeProjectFileService : IProjectFileService
{
    public ProjectData Data { get; set; } = new();

    public void Save(string path, ProjectData data)
    {
    }

    public ProjectData Load(string path) => Data;
}

internal sealed class ConfigurableDxfGeometryService : IDxfGeometryService
{
    private readonly Func<string, DxfContour> _factory;

    public ConfigurableDxfGeometryService(Rect bounds)
        : this(_ => new() { Bounds = bounds, Quality = DxfGeometryQuality.Exact, Diagnostics = new List<DxfGeometryDiagnostic>() })
    {
    }

    public ConfigurableDxfGeometryService(Func<string, DxfContour> factory)
    {
        _factory = factory;
    }

    public DxfContour ReadContour(string path) => _factory(path);
}

internal sealed class FakeNestingEngine : INestingEngine
{
    public NestingResult Nest(IReadOnlyList<Part> parts, IReadOnlyList<SheetMaterial> materials, NestingOptions options,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        => new();
}

internal sealed class RecordingNestingEngine : INestingEngine
{
    public List<Part> ReceivedParts { get; } = new();

    public NestingResult Nest(IReadOnlyList<Part> parts, IReadOnlyList<SheetMaterial> materials, NestingOptions options,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ReceivedParts.AddRange(parts);
        return new NestingResult();
    }
}

internal sealed class FakeDxfGeometryService : IDxfGeometryService
{
    public DxfContour ReadContour(string path) => new()
    {
        Bounds = new Rect(0, 0, 10, 10),
        Quality = DxfGeometryQuality.Exact,
        Diagnostics = new List<DxfGeometryDiagnostic>()
    };
}

internal sealed class ThrowingDxfGeometryService : IDxfGeometryService
{
    public DxfContour ReadContour(string path) => throw new InvalidOperationException("bad geometry");
}

internal sealed class FakeDxfExportService : IDxfExportService
{
    public ManualResetEventSlim Gate { get; } = new(true);
    public ManualResetEventSlim CombinedGate { get; } = new(true);
    public int EntryCount { get; private set; }
    public int WriteSheetsCallCount { get; private set; }
    public List<string> WrittenPaths { get; } = new();
    public string? LastCombinedPath { get; private set; }
    public IReadOnlyList<SheetInstance>? LastCombinedSheets { get; private set; }
    public DxfExportLabels? LastCombinedLabels { get; private set; }

    public void WriteSheet(string outputPath, SheetInstance sheet, double partGapMm, double sheetMarginMm, DxfExportLabels? labels = null, CancellationToken cancellationToken = default)
    {
        EntryCount++;
        Gate.Wait();
        WrittenPaths.Add(outputPath);
    }

    public void WriteSheets(IReadOnlyList<(string OutputPath, SheetInstance Sheet)> sheets, double partGapMm, double sheetMarginMm, DxfExportLabels? labels = null, CancellationToken cancellationToken = default)
    {
        WriteSheetsCallCount++;
        foreach (var item in sheets)
            WriteSheet(item.OutputPath, item.Sheet, partGapMm, sheetMarginMm, labels, cancellationToken);
    }

    public void WriteCombined(string outputPath, IReadOnlyList<SheetInstance> sheets, double partGapMm, double sheetMarginMm, DxfExportLabels labels, CancellationToken cancellationToken = default)
    {
        EntryCount++;
        LastCombinedPath = outputPath;
        LastCombinedSheets = sheets;
        LastCombinedLabels = labels;
        CombinedGate.Wait();
    }
}

internal sealed class FakeReportService : IReportService
{
    public string? LastOutputPath { get; private set; }
    public ReportLabels? LastLabels { get; private set; }

    public void GenerateReport(string outputPath, string projectName, IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials, IReadOnlyList<SheetInstance> sheets, IReadOnlyList<byte[]> sheetThumbnails,
        ReportLabels? labels = null, ReportContentOptions? options = null, CancellationToken cancellationToken = default)
    {
        LastOutputPath = outputPath;
        LastLabels = labels;
    }
}

internal sealed class FakeExportFileNameService : IExportFileNameService
{
    private readonly DateTime _fixedTimestamp = new(2025, 4, 15, 10, 30, 0);

    public int TimestampRequestCount { get; private set; }
    public List<DateTime> DxfTimestamps { get; } = new();
    public List<string> DxfPaths { get; } = new();

    public DateTime GetBatchTimestamp()
    {
        TimestampRequestCount++;
        return _fixedTimestamp;
    }

    public string GetDxfExportPath(string outputDirectory, SheetInstance sheet)
        => GetDxfExportPath(outputDirectory, sheet, _fixedTimestamp);

    public string GetDxfExportPath(string outputDirectory, SheetInstance sheet, DateTime timestamp)
    {
        DxfTimestamps.Add(timestamp);
        var path = Path.Combine(outputDirectory, $"{sheet.Material.Name}_{timestamp:yyyyMMdd}_{sheet.Index}.dxf");
        DxfPaths.Add(path);
        return path;
    }

    public string GetCombinedDxfExportPath(string outputDirectory)
        => GetCombinedDxfExportPath(outputDirectory, _fixedTimestamp);

    public string GetCombinedDxfExportPath(string outputDirectory, DateTime timestamp)
        => Path.Combine(outputDirectory, $"Combined_{timestamp:yyyyMMdd}.dxf");

    public string GetReportExportPath(string outputDirectory)
        => Path.Combine(outputDirectory, "Report.docx");

    public string GetReportExportPath(string outputDirectory, DateTime timestamp)
        => Path.Combine(outputDirectory, $"Report_{timestamp:yyyyMMdd_HHmmss}.docx");
}
