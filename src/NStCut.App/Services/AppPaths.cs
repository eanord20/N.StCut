using System.IO;

namespace NStCut.App.Services;

/// <summary>
/// Общая точка для путей хранения пользовательских данных приложения — устраняет дублирование
/// "%AppData%\NStCut" в нескольких сервисах (см. docs/Audit_Architecture.md, issue A3).
/// </summary>
public static class AppPaths
{
    public static string RootFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NStCut");
}
