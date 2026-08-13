using System.Collections.ObjectModel;
using NStCut.App.Models;

namespace NStCut.App.ViewModels;

public sealed class MaterialsViewModel
{
    public MaterialsViewModel(ObservableCollection<MaterialRow> materials)
    {
        Materials = materials;
    }

    public ObservableCollection<MaterialRow> Materials { get; }
}
