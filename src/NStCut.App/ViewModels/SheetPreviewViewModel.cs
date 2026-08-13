using NStCut.Core.Models;

namespace NStCut.App.ViewModels;

public sealed class SheetPreviewViewModel
{
    public SheetPreviewViewModel(NestingResult result)
    {
        Result = result;
    }

    public NestingResult Result { get; }
}
