using NStCut.Core.Models;

namespace NStCut.Core.Tests;

public class DroppedDxfMapperTests
{
    [Theory]
    [InlineData("part.dxf", true)]
    [InlineData("part.DXF", true)]
    [InlineData("part.Dxf", true)]
    [InlineData("part.txt", false)]
    [InlineData("part", false)]
    public void IsDxfFile_OnlyChecksExtensionCaseInsensitively(string path, bool expected)
    {
        Assert.Equal(expected, DroppedDxfMapper.IsDxfFile(path));
    }

    [Fact]
    public void IsDxfFile_NullPath_IsFalse()
    {
        Assert.False(DroppedDxfMapper.IsDxfFile(null));
    }

    [Fact]
    public void FilterDxfFiles_KeepsOrderAndDropsNonDxf()
    {
        var input = new[] { "a.dxf", "b.txt", "c.DXF", "d.pdf", null };

        var result = DroppedDxfMapper.FilterDxfFiles(input);

        Assert.Equal(new[] { "a.dxf", "c.DXF" }, result);
    }

    [Fact]
    public void Map_FromFullPath_UsesPartNameParserOnFileName()
    {
        var (number, title, quantity) = DroppedDxfMapper.Map(@"C:\Folder\2835.01.008 Cover.dxf");

        Assert.Equal("2835.01.008", number);
        Assert.Equal("Cover", title);
        Assert.Equal(1, quantity);
    }

    [Fact]
    public void Map_FromFileNameWithoutNumber_ReturnsWholeNameAsTitle()
    {
        var (number, title, quantity) = DroppedDxfMapper.Map("Cover.dxf");

        Assert.Equal(string.Empty, number);
        Assert.Equal("Cover", title);
        Assert.Equal(1, quantity);
    }
}
