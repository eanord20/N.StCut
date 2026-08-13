using System.IO;
using System.Text.Json;
using NStCut.App.Models;

namespace NStCut.App.Services;

public sealed class ProjectFileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Save(string path, ProjectData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, SerializerOptions));
    }

    public ProjectData Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ProjectData>(json, SerializerOptions) ?? new ProjectData();
    }
}
