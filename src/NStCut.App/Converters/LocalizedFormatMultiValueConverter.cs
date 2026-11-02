using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NStCut.App.Converters;

public sealed class LocalizedFormatMultiValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values.Any(value => value == DependencyProperty.UnsetValue || value == System.Windows.Data.Binding.DoNothing))
            return DependencyProperty.UnsetValue;

        if (values[^1] is not string format)
            return DependencyProperty.UnsetValue;

        return string.Format(culture, format, values[..^1]);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
