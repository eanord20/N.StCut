using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NStCut.App.Tests;


public class ThemeResourceTests
{
    private const string LightThemePath = "AppTheme.xaml";
    private const string DarkThemePath = "AppThemeDark.xaml";

    private static readonly string[] RequiredBrushKeys =
    {
        "AppMenuBackgroundBrush",
        "AppMenuForegroundBrush",
        "AppInputBackgroundBrush",
        "AppInputForegroundBrush",
        "AppInputDropDownBackgroundBrush",
        "MaterialDesignBody",
        "MaterialDesignTextBoxBackground",
    };

    private static ResourceDictionary LoadDictionary(string relativePath)
    {
        var uri = new Uri($"/NStCut.App;component/{relativePath}", UriKind.Relative);
        return (ResourceDictionary)System.Windows.Application.LoadComponent(uri);
    }

    [Theory]
    [InlineData(LightThemePath)]
    [InlineData(DarkThemePath)]
    public void RequiredContrastBrushesExist(string path)
    {
        var dict = LoadDictionary(path);
        var stringKeys = dict.Keys.OfType<string>().ToHashSet();
        foreach (var key in RequiredBrushKeys)
        {
            Assert.Contains(key, stringKeys);
            var brush = dict[key];
            Assert.IsType<SolidColorBrush>(brush);
        }
    }

    [Theory]
    [InlineData(LightThemePath)]
    [InlineData(DarkThemePath)]
    public void ForegroundAndBackgroundBrushesDiffer(string path)
    {
        var dict = LoadDictionary(path);
        var background = (SolidColorBrush)dict["AppMenuBackgroundBrush"];
        var foreground = (SolidColorBrush)dict["AppMenuForegroundBrush"];

        Assert.NotEqual(background.Color, foreground.Color);

        var inputBackground = (SolidColorBrush)dict["AppInputBackgroundBrush"];
        var inputForeground = (SolidColorBrush)dict["AppInputForegroundBrush"];

        Assert.NotEqual(inputBackground.Color, inputForeground.Color);
    }

    [Theory]
    [InlineData(LightThemePath)]
    [InlineData(DarkThemePath)]
    public void BodyAndTextBoxBrushesContrast(string path)
    {
        var dict = LoadDictionary(path);
        var body = (SolidColorBrush)dict["MaterialDesignBody"];
        var textBoxBack = (SolidColorBrush)dict["MaterialDesignTextBoxBackground"];
        var windowBack = (SolidColorBrush)dict["WindowBackgroundBrush"];

        Assert.NotEqual(body.Color, windowBack.Color);
        Assert.NotEqual(textBoxBack.Color, body.Color);
    }

    [Fact]
    public void LightAndDarkDictionariesContainSameKeys()
    {
        var light = LoadDictionary(LightThemePath);
        var dark = LoadDictionary(DarkThemePath);

        var lightKeys = light.Keys.OfType<string>().OrderBy(k => k).ToList();
        var darkKeys = dark.Keys.OfType<string>().OrderBy(k => k).ToList();

        Assert.Equal(lightKeys, darkKeys);
    }

    [Theory]
    [InlineData(LightThemePath)]
    [InlineData(DarkThemePath)]
    public void AppMaterialComboBoxStyleExists(string path)
    {
        var dict = LoadDictionary(path);
        var stringKeys = dict.Keys.OfType<string>().ToHashSet();
        Assert.Contains("AppMaterialComboBoxStyle", stringKeys);
        var style = (Style)dict["AppMaterialComboBoxStyle"];
        Assert.Equal(typeof(System.Windows.Controls.ComboBox), style.TargetType);
    }

    [Theory]
    [InlineData(LightThemePath)]
    [InlineData(DarkThemePath)]
    public void AppMaterialComboBoxStyleHasItemContainerStyle(string path)
    {
        var dict = LoadDictionary(path);
        var style = (Style)dict["AppMaterialComboBoxStyle"];
        var itemContainerSetter = style.Setters
            .OfType<Setter>()
            .FirstOrDefault(s => s.Property.Name == "ItemContainerStyle");
        Assert.NotNull(itemContainerSetter);
        Assert.IsType<Style>(itemContainerSetter.Value);
    }

    [Theory]
    [InlineData(LightThemePath)]
    [InlineData(DarkThemePath)]
    public void MenuStyleUsesAppContrastResources(string path)
    {
        var dict = LoadDictionary(path);
        var menuStyle = (Style)dict[typeof(Menu)];
        var menuItemStyle = (Style)dict[typeof(MenuItem)];

        Assert.Contains(menuStyle.Setters.OfType<Setter>(),
            s => s.Property.Name == "Background" && ((s.Value as DynamicResourceExtension)?.ResourceKey?.ToString() == "AppMenuBackgroundBrush"));
        Assert.Contains(menuStyle.Setters.OfType<Setter>(),
            s => s.Property.Name == "Foreground" && ((s.Value as DynamicResourceExtension)?.ResourceKey?.ToString() == "AppMenuForegroundBrush"));

        Assert.Contains(menuItemStyle.Setters.OfType<Setter>(),
            s => s.Property.Name == "Background" && ((s.Value as DynamicResourceExtension)?.ResourceKey?.ToString() == "AppMenuBackgroundBrush"));
        Assert.Contains(menuItemStyle.Setters.OfType<Setter>(),
            s => s.Property.Name == "Foreground" && ((s.Value as DynamicResourceExtension)?.ResourceKey?.ToString() == "AppMenuForegroundBrush"));

        var highlightTrigger = menuItemStyle.Triggers.OfType<Trigger>().FirstOrDefault(t => t.Property == MenuItem.IsHighlightedProperty);
        Assert.NotNull(highlightTrigger);
        Assert.Contains(highlightTrigger.Setters.OfType<Setter>(),
            s => s.Property.Name == "Background" && ((s.Value as DynamicResourceExtension)?.ResourceKey?.ToString() == "AppInputBackgroundBrush"));
        Assert.Contains(highlightTrigger.Setters.OfType<Setter>(),
            s => s.Property.Name == "Foreground" && ((s.Value as DynamicResourceExtension)?.ResourceKey?.ToString() == "AppInputForegroundBrush"));
    }

    private static readonly string[] StockMenuBrushKeys =
    {
        "SubMenuBackgroundBrush",
        "Menu.Static.Background",
        "Menu.Static.Foreground",
        "MenuItem.Highlight.Background",
        "MenuItem.Highlight.Border",
        "MenuItem.Highlight.Disabled.Background",
        "MenuItem.Highlight.Disabled.Border",
        "MenuItem.Selected.Background",
        "MenuItem.Selected.Border",
    };

    [Theory]
    [InlineData(LightThemePath)]
    [InlineData(DarkThemePath)]
    public void StockMenuTemplateBrushesAreAliasedAndContrast(string path)
    {
        var dict = LoadDictionary(path);
        var stringKeys = dict.Keys.OfType<string>().ToHashSet();
        var menuBackground = ((SolidColorBrush)dict["AppMenuBackgroundBrush"]).Color;
        var menuForeground = ((SolidColorBrush)dict["AppMenuForegroundBrush"]).Color;
        var inputBackground = ((SolidColorBrush)dict["AppInputBackgroundBrush"]).Color;
        var inputForeground = ((SolidColorBrush)dict["AppInputForegroundBrush"]).Color;

        foreach (var key in StockMenuBrushKeys)
        {
            Assert.Contains(key, stringKeys);
            var brush = (SolidColorBrush)dict[key];
            Assert.NotNull(brush);

            // popup/highlight/selected resources must differ from the text they frame
            if (key is "SubMenuBackgroundBrush" or "Menu.Static.Background" or "MenuItem.Highlight.Disabled.Background")
            {
                Assert.NotEqual(brush.Color, menuForeground);
            }
            else
            {
                Assert.NotEqual(brush.Color, key.Contains("Border") ? inputBackground : menuBackground);
            }
        }
    }

    [Theory]
    [InlineData(LightThemePath)]
    [InlineData(DarkThemePath)]
    public void ContextMenuStyleUsesAppResources(string path)
    {
        var dict = LoadDictionary(path);
        var contextMenuStyle = (Style)dict[typeof(ContextMenu)];

        Assert.Contains(contextMenuStyle.Setters.OfType<Setter>(),
            s => s.Property.Name == "Background" && ((s.Value as DynamicResourceExtension)?.ResourceKey?.ToString() == "AppMenuBackgroundBrush"));
        Assert.Contains(contextMenuStyle.Setters.OfType<Setter>(),
            s => s.Property.Name == "Foreground" && ((s.Value as DynamicResourceExtension)?.ResourceKey?.ToString() == "AppMenuForegroundBrush"));
    }

    [Fact]
    public void MainWindowXamlDoesNotHardcodeBrushesOrColors()
    {
        var xamlPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "..", "..", "..", "..", "NStCut.App", "MainWindow.xaml");
        xamlPath = Path.GetFullPath(xamlPath);
        Assert.True(File.Exists(xamlPath), $"MainWindow.xaml not found at {xamlPath}");

        var xaml = File.ReadAllText(xamlPath);

        Assert.DoesNotContain("Color=\"#", xaml);
        Assert.DoesNotContain("Foreground=\"White\"", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Foreground=\"Black\"", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Background=\"White\"", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Background=\"Black\"", xaml, StringComparison.OrdinalIgnoreCase);
    }
}
