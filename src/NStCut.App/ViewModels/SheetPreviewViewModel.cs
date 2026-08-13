using System.Collections.ObjectModel;
using NStCut.Core.Models;

namespace NStCut.App.ViewModels;

public sealed class SheetPreviewViewModel
{
    public ObservableCollection<SheetInstance> Sheets { get; } = new();

    public SheetPreviewViewModel(NestingResult result)
    {
        foreach (var sheet in result.Sheets)
            Sheets.Add(sheet);
    }
}
