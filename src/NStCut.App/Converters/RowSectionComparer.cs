using System.Collections;
using NStCut.App.Models;
using NStCut.App.ViewModels;

namespace NStCut.App.Converters;

/// <summary>Сортирует строки таблицы деталей: сначала по порядку материала в проекте, затем по номеру в разделе.</summary>
internal sealed class RowSectionComparer : IComparer
{
    private readonly MainViewModel _owner;

    public RowSectionComparer(MainViewModel owner) => _owner = owner;

    public int Compare(object? x, object? y)
    {
        if (x is not PartRow rowX || y is not PartRow rowY) return 0;
        var orderCompare = _owner.GetMaterialOrder(rowX.MaterialId).CompareTo(_owner.GetMaterialOrder(rowY.MaterialId));
        return orderCompare != 0 ? orderCompare : rowX.RowNumberInSection.CompareTo(rowY.RowNumberInSection);
    }
}
