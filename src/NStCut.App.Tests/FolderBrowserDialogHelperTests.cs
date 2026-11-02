using System.IO;
using NStCut.App.Services;

namespace NStCut.App.Tests;

public class FolderBrowserDialogHelperTests
{
    [Fact]
    public void GetInitialFolder_ReturnsExistingPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = FolderBrowserDialogHelper.GetInitialFolder(tempDir);

            Assert.Equal(tempDir, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetInitialFolder_ReturnsEmpty_WhenPathIsNullOrWhitespace(string? path)
    {
        var result = FolderBrowserDialogHelper.GetInitialFolder(path!);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetInitialFolder_ReturnsEmpty_WhenPathDoesNotExist()
    {
        var nonExistingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing");

        var result = FolderBrowserDialogHelper.GetInitialFolder(nonExistingPath);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetInitialFolder_ReturnsEmpty_WhenPathIsAFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var result = FolderBrowserDialogHelper.GetInitialFolder(tempFile);

            Assert.Equal(string.Empty, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
