using System.IO;
using NStCut.App.Models;
using NStCut.App.Services;
using NStCut.Core.Constants;

namespace NStCut.App.Tests;

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public AppSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
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

    private string SettingsPath => Path.Combine(_tempDir, "app-settings.json");

    private AppSettingsService CreateService() => new(SettingsPath);

    [Fact]
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var service = CreateService();
        var settings = service.Load();
        Assert.NotNull(settings);
        Assert.Equal(string.Empty, settings.SearchPath);
        Assert.Equal(string.Empty, settings.OutputPath);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsSearchAndOutputPaths()
    {
        var service = CreateService();
        var original = new AppSettings
        {
            SearchPath = @"C:\dxf",
            OutputPath = @"C:\out",
            SheetMarginMm = 7.5,
            PartGapMm = 3.0,
            Culture = "ua",
            ExportCombinedDxf = true
        };

        service.Save(original);
        var loaded = service.Load();

        Assert.Equal(original.SearchPath, loaded.SearchPath);
        Assert.Equal(original.OutputPath, loaded.OutputPath);
        Assert.Equal(original.SheetMarginMm, loaded.SheetMarginMm);
        Assert.Equal(original.PartGapMm, loaded.PartGapMm);
        Assert.Equal(original.Culture, loaded.Culture);
        Assert.True(loaded.ExportCombinedDxf);
    }

    [Fact]
    public void SaveThenLoad_PreservesExplicitZeroMarginAndGap()
    {
        var service = CreateService();
        service.Save(new AppSettings { SheetMarginMm = 0, PartGapMm = 0 });

        var loaded = service.Load();

        Assert.Equal(0, loaded.SheetMarginMm);
        Assert.Equal(0, loaded.PartGapMm);
    }

    [Fact]
    public void Load_PartialJsonRetainsDefaultMarginAndGap()
    {
        File.WriteAllText(SettingsPath, "{\"culture\":\"ua\"}");

        var loaded = CreateService().Load();

        Assert.Equal(DefaultNestingOptions.Value.SheetMarginMm, loaded.SheetMarginMm);
        Assert.Equal(DefaultNestingOptions.Value.PartGapMm, loaded.PartGapMm);
    }

    [Fact]
    public void Save_CreatesDirectory_WhenItDoesNotExist()
    {
        var nestedDir = Path.Combine(_tempDir, "nested");
        var path = Path.Combine(nestedDir, "settings.json");
        var service = new AppSettingsService(path);
        service.Save(new AppSettings { SearchPath = @"C:\dxf" });

        Assert.True(Directory.Exists(nestedDir));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupted()
    {
        var service = CreateService();
        File.WriteAllText(SettingsPath, "not json");
        var settings = service.Load();
        Assert.NotNull(settings);
        Assert.Equal(string.Empty, settings.SearchPath);
    }
}
