using NStCut.App.Models;

namespace NStCut.App.Services;

/// <summary>Абстракция над AppSettingsService — нужна для DIP/тестируемости MainViewModel (см. docs/Audit_Architecture.md, SOLID Assessment, D).</summary>
public interface IAppSettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
