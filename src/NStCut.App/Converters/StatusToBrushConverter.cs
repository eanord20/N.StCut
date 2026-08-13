using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using NStCut.Core.Models;

namespace NStCut.App.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not PartStatus status)
            return Brushes.Transparent;

        return status switch
        {
            PartStatus.Found => Brushes.LightGreen,
            PartStatus.ManuallySelected => Brushes.LightYellow,
            PartStatus.NotFound => Brushes.Orange,
            PartStatus.Ambiguous => Brushes.LightBlue,
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
