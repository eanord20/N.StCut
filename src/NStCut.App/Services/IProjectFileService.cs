using NStCut.App.Models;

namespace NStCut.App.Services;

/// <summary>Абстракция над ProjectFileService — нужна для DIP/тестируемости MainViewModel (см. docs/Audit_Architecture.md, SOLID Assessment, D).</summary>
public interface IProjectFileService
{
    void Save(string path, ProjectData data);
    ProjectData Load(string path);

    /// <summary>Возвращает false, если файл проекта повреждён/несовместим.</summary>
    bool TryLoad(string path, out ProjectData data)
    {
        data = Load(path);
        return true;
    }
}
