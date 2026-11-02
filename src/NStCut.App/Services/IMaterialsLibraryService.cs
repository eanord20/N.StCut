using NStCut.Core.Models;

namespace NStCut.App.Services;

/// <summary>Абстракция над MaterialsLibraryService — нужна для DIP/тестируемости MaterialsViewModel (см. docs/Audit_Architecture.md, SOLID Assessment, D).</summary>
public interface IMaterialsLibraryService
{
    List<SheetMaterial> Load();
    void Save(IReadOnlyList<SheetMaterial> materials);
}
