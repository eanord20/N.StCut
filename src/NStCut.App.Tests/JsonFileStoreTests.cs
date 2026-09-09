using System.IO;
using NStCut.App.Services;
using Xunit;

namespace NStCut.App.Tests;

public class JsonFileStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public JsonFileStoreTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    [Fact]
    public void WriteAtomic_CreatesFile_WhenTargetMissing()
    {
        var path = Path.Combine(_tempDir, "target.json");

        JsonFileStore.WriteAtomic(path, "content");

        Assert.Equal("content", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void WriteAtomic_ReplacesExistingFile_AndCleansTemp()
    {
        var path = Path.Combine(_tempDir, "target.json");
        File.WriteAllText(path, "old");

        JsonFileStore.WriteAtomic(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void WriteAtomic_CleansStaleTempBeforeWriting()
    {
        var path = Path.Combine(_tempDir, "target.json");
        File.WriteAllText(path + ".tmp", "stale");

        JsonFileStore.WriteAtomic(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void WriteAtomic_CreatesMissingDirectory()
    {
        var path = Path.Combine(_tempDir, "nested", "target.json");

        JsonFileStore.WriteAtomic(path, "content");

        Assert.Equal("content", File.ReadAllText(path));
    }
}
