using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows.Data;
using NStCut.App.Models;

namespace NStCut.App.Converters;

/// <summary>Преобразует MaterialId строки в название материала для заголовка группы (раздела) в таблице.</summary>
internal sealed class MaterialIdToNameConverter : IValueConverter
{
    private readonly ObservableCollection<MaterialRow> _materials;

    public MaterialIdToNameConverter(ObservableCollection<MaterialRow> materials) => _materials = materials;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var materialId = value as string;
        var material = _materials.FirstOrDefault(m => m.Id == materialId);
        return material?.Name ?? "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
