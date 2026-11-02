using System.IO;
using System.Text.Json;
using NStCut.App.Models;

namespace NStCut.App.Services;

/// <summary>
/// Хранит настройки приложения (папка поиска DXF, папка сохранения, параметры раскроя, язык)
/// между запусками — не привязаны к конкретному .nstcut-проекту.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private readonly string _settingsPath;

    public AppSettingsService()
        : this(Path.Combine(AppPaths.RootFolder, "app-settings.json"))
    {
    }

    public AppSettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonFileStore.SerializerOptions) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Повреждённый/несовместимый файл настроек не должен мешать запуску приложения.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
        => JsonFileStore.WriteAtomic(_settingsPath, JsonSerializer.Serialize(settings, JsonFileStore.SerializerOptions));
}
