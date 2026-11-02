using System.IO;

namespace NStCut.Core.Models;

public static class DroppedDxfMapper
{
    public static bool IsDxfFile(string? path)
        => !string.IsNullOrWhiteSpace(path) &&
           string.Equals(Path.GetExtension(path), ".dxf", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> FilterDxfFiles(IEnumerable<string?> paths)
        => paths.OfType<string>().Where(IsDxfFile).ToList();

    public static (string Number, string Title, int Quantity) Map(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var (number, title) = PartNameParser.Split(fileName);
        return (number, title, 1);
    }
}
