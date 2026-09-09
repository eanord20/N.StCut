using System.IO;
using System.Text.Json;

namespace NStCut.App.Services;

/// <summary>
/// Общая точка для чтения/записи JSON-файлов настроек приложения — единая политика сериализации
/// вместо трёх независимых копий (см. docs/Audit_Architecture.md, issue A5) и атомарная запись
/// (запись во временный файл + замена целевого), чтобы сбой посреди записи не оставлял
/// повреждённый файл (см. docs/Audit_Reliability.md, issue R4).
/// </summary>
public static class JsonFileStore
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void WriteAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        try
        {
            File.WriteAllText(tempPath, content);

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
