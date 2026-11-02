using System.IO;
using NStCut.App.Services;

namespace NStCut.App.Tests;

public class ProjectFileServiceTests : IDisposable
{
    private readonly string _tempFile;

    public ProjectFileServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), "NStCutTests_" + Guid.NewGuid().ToString("N") + ".nstcut");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void Load_MigratesLegacyNameField_IntoNumberAndTitle()
    {
        // Формат проектов до разделения Part.Name на Number/Title.
        const string legacyJson = """
        {
          "searchPath": "C:\\dxf",
          "outputPath": "C:\\out",
          "parts": [
            { "name": "2835.01.008 Отбортовка", "quantity": 1, "materialId": "", "dxfPath": "", "status": 0, "isManuallyResolved": false, "grainMode": 0 }
          ],
          "materials": []
        }
        """;
        File.WriteAllText(_tempFile, legacyJson);
        var service = new ProjectFileService();

        var data = service.Load(_tempFile);

        Assert.Single(data.Parts);
        Assert.Equal("2835.01.008", data.Parts[0].Number);
        Assert.Equal("Отбортовка", data.Parts[0].Title);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsNumberAndTitle()
    {
        var service = new ProjectFileService();
        var data = new Models.ProjectData
        {
            SearchPath = "C:\\dxf",
            OutputPath = "C:\\out",
            Parts = { new Core.Models.Part { Number = "2835.01.008", Title = "Отбортовка", Quantity = 2 } }
        };

        service.Save(_tempFile, data);
        var loaded = service.Load(_tempFile);

        Assert.Single(loaded.Parts);
        Assert.Equal("2835.01.008", loaded.Parts[0].Number);
        Assert.Equal("Отбортовка", loaded.Parts[0].Title);
        Assert.Equal(2, loaded.Parts[0].Quantity);
    }
}
