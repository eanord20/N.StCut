using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using NStCut.Core.Models;

namespace NStCut.App.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not PartStatus status)
            return Brushes.Transparent;

        // Цвета берутся из AppTheme.xaml (единая система ресурсов, см. docs/Audit_Architecture.md, issue A1)
        // вместо захардкоженных Brushes.* — это же позволяет переключению темы (Light/Dark) в будущем
        // менять цвета статуса строк вместе с остальной палитрой.
        var resourceKey = status switch
        {
            PartStatus.Found => "FoundRowBrush",
            PartStatus.ManuallySelected => "ManuallySelectedRowBrush",
            PartStatus.NotFound => "NotFoundRowBrush",
            PartStatus.Ambiguous => "AmbiguousRowBrush",
            _ => null
        };

        if (resourceKey == null)
            return Brushes.Transparent;

        return (Brush?)Application.Current.TryFindResource(resourceKey) ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
