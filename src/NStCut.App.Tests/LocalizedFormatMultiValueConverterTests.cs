using System.Globalization;
using System.Windows;
using NStCut.App.Converters;

namespace NStCut.App.Tests;

public class LocalizedFormatMultiValueConverterTests
{
    private readonly LocalizedFormatMultiValueConverter _converter = new();

    [Fact]
    public void Convert_UsesLastValueAsEnglishFormat()
    {
        var result = _converter.Convert(
            new object[] { 2, "Sheet {0}" },
            typeof(string),
            null!,
            CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Sheet 2", result);
    }

    [Fact]
    public void Convert_UsesLastValueAsUkrainianFormatAndCurrentCultureForNumbers()
    {
        var result = _converter.Convert(
            new object[] { "Сталь", 1000, 500, 12.34, "{0}  {1}x{2} мм  використання {3:F1}%" },
            typeof(string),
            null!,
            CultureInfo.GetCultureInfo("uk-UA"));

        Assert.Equal("Сталь  1000x500 мм  використання 12,3%", result);
    }

    [Fact]
    public void Convert_ReturnsUnsetValue_WhenAnyValueIsUnset()
    {
        var result = _converter.Convert(
            new object[] { 1, DependencyProperty.UnsetValue, "Sheet {0}" },
            typeof(string),
            null!,
            CultureInfo.InvariantCulture);

        Assert.Same(DependencyProperty.UnsetValue, result);
    }
}
