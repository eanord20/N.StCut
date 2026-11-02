using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using NStCut.App.Models;
using NStCut.Core.Models;

namespace NStCut.App.Services;

public sealed class ProjectFileService : IProjectFileService
{
    public void Save(string path, ProjectData data)
        => JsonFileStore.WriteAtomic(path, JsonSerializer.Serialize(data, JsonFileStore.SerializerOptions));

    public ProjectData Load(string path)
        => TryLoad(path, out var data) ? data : new ProjectData();

    public bool TryLoad(string path, out ProjectData data)
    {
        try
        {
            var json = File.ReadAllText(path);
            var migratedJson = MigrateLegacyPartNames(json);
            data = JsonSerializer.Deserialize<ProjectData>(migratedJson, JsonFileStore.SerializerOptions) ?? new ProjectData();
            return true;
        }
        catch (Exception)
        {
            // Повреждённый/несовместимый файл проекта не должен приводить к падению приложения
            // (см. AppSettingsService.Load — тот же защитный паттерн).
            data = new ProjectData();
            return false;
        }
    }

    /// <summary>
    /// Проекты, сохранённые до разделения Part.Name на Number/Title, хранили единственное
    /// поле "name". Разбираем его эвристикой PartNameParser, чтобы старые .nstcut открывались корректно.
    /// </summary>
    private static string MigrateLegacyPartNames(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject();
        if (root?["parts"] is not JsonArray parts)
            return json;

        foreach (var partNode in parts)
        {
            if (partNode is not JsonObject partObj) continue;
            if (partObj.ContainsKey("number") || partObj.ContainsKey("title")) continue;
            var legacyName = partObj["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(legacyName)) continue;
            var (number, title) = PartNameParser.Split(legacyName);
            partObj["number"] = number;
            partObj["title"] = title;
        }

        return root.ToJsonString(JsonFileStore.SerializerOptions);
    }
}
