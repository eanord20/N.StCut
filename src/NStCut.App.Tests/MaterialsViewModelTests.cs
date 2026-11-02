using NStCut.App.Models;
using NStCut.App.Services;
using NStCut.App.ViewModels;
using NStCut.Core.Models;

namespace NStCut.App.Tests;

public class MaterialsViewModelTests
{
    private static MaterialsViewModel CreateViewModel(IEnumerable<SheetMaterial>? library = null, IEnumerable<MaterialRow>? project = null)
    {
        var service = new FakeMaterialsLibraryService(library);
        return new MaterialsViewModel(project ?? Array.Empty<MaterialRow>(), service);
    }

    [Fact]
    public void CopyCommand_CanExecute_WhenLibraryMaterialSelected()
    {
        var vm = CreateViewModel(new[] { new SheetMaterial { Name = "Steel" } });

        Assert.False(vm.CopyCommand.CanExecute(null));

        vm.SelectedLibraryMaterial = vm.LibraryMaterials[0];

        Assert.True(vm.CopyCommand.CanExecute(null));
    }

    [Fact]
    public void CopyCommand_CreatesIndependentMaterial_WithNewId()
    {
        var original = new SheetMaterial { Name = "Steel", Length = 1000, Width = 500 };
        var vm = CreateViewModel(new[] { original });
        vm.SelectedLibraryMaterial = vm.LibraryMaterials[0];

        vm.CopyCommand.Execute(null);

        var copy = vm.LibraryMaterials[1];
        Assert.NotSame(vm.LibraryMaterials[0], copy);
        Assert.NotEqual(original.Id, copy.Id);
        Assert.False(string.IsNullOrEmpty(copy.Id));
    }

    [Fact]
    public void CopyCommand_CopiesAllFields()
    {
        var original = new SheetMaterial
        {
            Name = "Steel",
            Length = 1234,
            Width = 567,
            Thickness = 2.5,
            HasGrain = true,
            GrainDirection = GrainDirection.Vertical
        };
        var vm = CreateViewModel(new[] { original });
        vm.SelectedLibraryMaterial = vm.LibraryMaterials[0];

        vm.CopyCommand.Execute(null);

        var copy = vm.LibraryMaterials[1];
        Assert.Equal(original.Length, copy.Length);
        Assert.Equal(original.Width, copy.Width);
        Assert.Equal(original.Thickness, copy.Thickness);
        Assert.Equal(original.HasGrain, copy.HasGrain);
        Assert.Equal(original.GrainDirection, copy.GrainDirection);
    }

    [Fact]
    public void CopyCommand_Name_GetsFirstFreeNumericSuffix()
    {
        var vm = CreateViewModel(new[] { new SheetMaterial { Name = "Steel" } });
        vm.SelectedLibraryMaterial = vm.LibraryMaterials[0];

        vm.CopyCommand.Execute(null);

        Assert.Equal("Steel_1", vm.LibraryMaterials[1].Name);
    }

    [Fact]
    public void CopyCommand_Name_SkipsExistingSuffixes_CaseInsensitive()
    {
        var vm = CreateViewModel(new[]
        {
            new SheetMaterial { Name = "Steel" },
            new SheetMaterial { Name = "steel_1" },
            new SheetMaterial { Name = "STEEL_2" }
        });
        vm.SelectedLibraryMaterial = vm.LibraryMaterials[0];

        vm.CopyCommand.Execute(null);

        Assert.Equal("Steel_3", vm.SelectedLibraryMaterial!.Name);
    }

    [Fact]
    public void CopyCommand_DoesNotAddToProjectMaterials()
    {
        var original = new SheetMaterial { Name = "Steel" };
        var vm = CreateViewModel(new[] { original }, new[] { new MaterialRow { Id = original.Id, Name = original.Name } });
        vm.SelectedLibraryMaterial = vm.LibraryMaterials[0];

        vm.CopyCommand.Execute(null);

        Assert.Single(vm.ProjectMaterials);
        Assert.DoesNotContain(vm.LibraryMaterials[1], vm.ProjectMaterials);
    }

    [Fact]
    public void CopyCommand_InsertsCopyNextToOriginal_AndSelectsIt()
    {
        var vm = CreateViewModel(new[]
        {
            new SheetMaterial { Name = "Steel" },
            new SheetMaterial { Name = "Wood" }
        });
        var original = vm.LibraryMaterials[1];
        vm.SelectedLibraryMaterial = original;

        vm.CopyCommand.Execute(null);

        Assert.Equal(vm.LibraryMaterials.IndexOf(original) + 1, vm.LibraryMaterials.IndexOf(vm.SelectedLibraryMaterial));
        Assert.Equal("Wood_1", vm.SelectedLibraryMaterial!.Name);
    }

    [Fact]
    public void CopyCommand_DoesNotSaveLibrary()
    {
        var service = new FakeMaterialsLibraryService(new[] { new SheetMaterial { Name = "Steel" } });
        var vm = new MaterialsViewModel(Array.Empty<MaterialRow>(), service);
        vm.SelectedLibraryMaterial = vm.LibraryMaterials[0];

        vm.CopyCommand.Execute(null);

        Assert.False(service.SaveCalled);
    }

    [Fact]
    public void CopyCommand_DoesNothing_WhenNoMaterialSelected()
    {
        var vm = CreateViewModel(new[] { new SheetMaterial { Name = "Steel" } });

        Assert.False(vm.CopyCommand.CanExecute(null));

        vm.CopyCommand.Execute(null);

        Assert.Single(vm.LibraryMaterials);
        Assert.Null(vm.SelectedLibraryMaterial);
    }

    private sealed class FakeMaterialsLibraryService : IMaterialsLibraryService
    {
        private readonly List<SheetMaterial> _materials;
        public bool SaveCalled { get; private set; }

        public FakeMaterialsLibraryService(IEnumerable<SheetMaterial>? materials = null)
            => _materials = materials?.ToList() ?? new List<SheetMaterial>();

        public List<SheetMaterial> Load() => _materials.ToList();

        public void Save(IReadOnlyList<SheetMaterial> materials)
        {
            SaveCalled = true;
            _materials.Clear();
            _materials.AddRange(materials);
        }
    }
}
