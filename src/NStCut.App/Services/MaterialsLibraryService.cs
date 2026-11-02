using System.IO;
using System.Text.Json;
using NStCut.Core.Models;

namespace NStCut.App.Services;

/// <summary>
/// Хранит общую библиотеку материалов пользователя (не привязана к конкретному проекту .nstcut).
/// Проектный список материалов — это подмножество библиотеки, выбранное в окне "Материалы".
/// </summary>
public sealed class MaterialsLibraryService : IMaterialsLibraryService
{
    private static readonly string LibraryPath = Path.Combine(AppPaths.RootFolder, "materials-library.json");

    public List<SheetMaterial> Load()
    {
        if (!File.Exists(LibraryPath))
            return new List<SheetMaterial>();

        try
        {
            var json = File.ReadAllText(LibraryPath);
            return JsonSerializer.Deserialize<List<SheetMaterial>>(json, JsonFileStore.SerializerOptions) ?? new List<SheetMaterial>();
        }
        catch (Exception)
        {
            // Повреждённый/несовместимый файл библиотеки не должен приводить к падению приложения
            // (см. AppSettingsService.Load — тот же защитный паттерн).
            return new List<SheetMaterial>();
        }
    }

    public void Save(IReadOnlyList<SheetMaterial> materials)
        => JsonFileStore.WriteAtomic(LibraryPath, JsonSerializer.Serialize(materials, JsonFileStore.SerializerOptions));

    /// <summary>Загружает библиотеку из произвольного файла для импорта; бросает при повреждении.</summary>
    public static IReadOnlyList<SheetMaterial> LoadForImport(string path)
    {
        var materials = JsonSerializer.Deserialize<List<SheetMaterial>>(
            File.ReadAllText(path), JsonFileStore.SerializerOptions)
            ?? throw new InvalidDataException(path);
        Validate(materials);
        return materials;
    }

    /// <summary>Экспортирует библиотеку в произвольный файл.</summary>
    public static void SaveAll(string path, IReadOnlyList<SheetMaterial> materials)
        => JsonFileStore.WriteAtomic(path, JsonSerializer.Serialize(materials, JsonFileStore.SerializerOptions));

    public static void Validate(IEnumerable<SheetMaterial> materials)
    {
        if (materials.Any(m => m == null ||
            !double.IsFinite(m.Length) || m.Length <= 0 ||
            !double.IsFinite(m.Width) || m.Width <= 0 ||
            (m.Thickness is double t && (!double.IsFinite(t) || t < 0))))
            throw new InvalidDataException("Invalid material library file.");
    }
}
