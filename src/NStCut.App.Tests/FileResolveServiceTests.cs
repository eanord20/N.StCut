using System.IO;
using NStCut.App.Services;
using NStCut.Core.Models;

namespace NStCut.App.Tests;

public class FileResolveServiceTests : IDisposable
{
    private readonly string _tempDir;

    public FileResolveServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NStCutTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Resolve_ReturnsFound_WhenSingleMatchExists()
    {
        File.WriteAllText(Path.Combine(_tempDir, "2835.01.008 Отбортовка.dxf"), string.Empty);
        var service = new FileResolveService();

        var result = service.Resolve("2835.01.008 Отбортовка", _tempDir);

        Assert.Equal(PartStatus.Found, result.Status);
    }

    [Fact]
    public void Resolve_ReturnsAmbiguous_WhenMultipleMatchesExist()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "rev1"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "rev2"));
        File.WriteAllText(Path.Combine(_tempDir, "rev1", "2835.01.008 Отбортовка.dxf"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDir, "rev2", "2835.01.008 Отбортовка.dxf"), string.Empty);
        var service = new FileResolveService();

        var result = service.Resolve("2835.01.008 Отбортовка", _tempDir);

        Assert.Equal(PartStatus.Ambiguous, result.Status);
    }

    [Fact]
    public void Resolve_ExcludesOutputFolderNestedInSearchFolder()
    {
        // Папка сохранения вложена в папку поиска и содержит копию того же файла
        // (например, ранее экспортированный/тестовый файл) — она не должна мешать поиску.
        var outputDir = Path.Combine(_tempDir, "test");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(_tempDir, "2835.01.008 Отбортовка.dxf"), string.Empty);
        File.WriteAllText(Path.Combine(outputDir, "2835.01.008 Отбортовка.dxf"), string.Empty);
        var service = new FileResolveService();

        var result = service.Resolve("2835.01.008 Отбортовка", _tempDir, excludePath: outputDir);

        Assert.Equal(PartStatus.Found, result.Status);
    }

    [Fact]
    public void Resolve_ReturnsNotFound_WhenNoFilesMatch()
    {
        var service = new FileResolveService();

        var result = service.Resolve("2835.01.008 Отбортовка", _tempDir);

        Assert.Equal(PartStatus.NotFound, result.Status);
    }
}
