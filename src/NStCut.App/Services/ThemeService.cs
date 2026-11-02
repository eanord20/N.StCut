using System.Collections.ObjectModel;
using System.Windows;
using Application = System.Windows.Application;

namespace NStCut.App.Services;

public enum AppTheme
{
    Light,
    Dark
}

/// <summary>
/// Переключение темы Light/Dark во время работы приложения (см. docs/UI_Design_System.md, раздел 5;
/// docs/UX_Audit.md, "Список изменений по приоритету → Critical"). Подменяет базовый словарь
/// MaterialDesignThemes (Light.xaml/Dark.xaml) и собственный AppTheme.xaml/AppThemeDark.xaml
/// в App.Resources.MergedDictionaries.
/// </summary>
public static class ThemeService
{
    private const string BaseLightUri = "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml";
    private const string BaseDarkUri = "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml";
    private const string AppThemeLightUri = "/AppTheme.xaml";
    private const string AppThemeDarkUri = "/AppThemeDark.xaml";

    public static void Apply(AppTheme theme)
    {
        // Application.Current отсутствует вне запущенного WPF-приложения (например, в юнит-тестах,
        // которые создают MainViewModel напрямую) — в этом случае применять тему не к чему.
        if (Application.Current == null)
            return;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        ReplaceDictionary(dictionaries, "MaterialDesignTheme.Light.xaml", "MaterialDesignTheme.Dark.xaml",
            theme == AppTheme.Dark ? BaseDarkUri : BaseLightUri);
        ReplaceDictionary(dictionaries, "AppTheme.xaml", "AppThemeDark.xaml",
            theme == AppTheme.Dark ? AppThemeDarkUri : AppThemeLightUri);
    }

    public static bool TryParse(string? value, out AppTheme theme)
        => Enum.TryParse(value, ignoreCase: true, out theme);

    private static void ReplaceDictionary(Collection<ResourceDictionary> dictionaries, string lightMarker, string darkMarker, string newUri)
    {
        var existing = dictionaries.FirstOrDefault(d =>
            d.Source != null && (d.Source.OriginalString.Contains(lightMarker) || d.Source.OriginalString.Contains(darkMarker)));
        var newDictionary = new ResourceDictionary { Source = new Uri(newUri, UriKind.RelativeOrAbsolute) };

        if (existing != null)
            dictionaries[dictionaries.IndexOf(existing)] = newDictionary;
        else
            dictionaries.Add(newDictionary);
    }
}
