using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace NStCut.App.Converters;

/// <summary>
/// Подсвечивает чип материала / заголовок раздела, если он совпадает с "активным" материалом для вставки.
/// Принимает [0] = Id/Name текущего элемента, [1] = MainViewModel.ActivePasteMaterialId.
/// </summary>
public sealed class ActiveMaterialHighlightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string id || values[1] is not string activeId)
            return Brushes.Transparent;

        return string.Equals(id, activeId, StringComparison.Ordinal)
            ? (Brush)Application.Current.TryFindResource("ActiveSectionBrush") ?? Brushes.LightBlue
            : Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
