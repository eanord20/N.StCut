using System.IO;

namespace NStCut.App.Services;

/// <summary>
/// Определяет начальную папку для FolderBrowserDialog на основе ранее сохранённого пути.
/// Несуществующий или пустой путь заменяется пустой строкой, чтобы диалог открылся без ошибок.
/// </summary>
public static class FolderBrowserDialogHelper
{
    public static string GetInitialFolder(string savedPath)
    {
        if (string.IsNullOrWhiteSpace(savedPath))
            return string.Empty;

        return Directory.Exists(savedPath) ? savedPath : string.Empty;
    }
}
