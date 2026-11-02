using NStCut.Core.Models;

namespace NStCut.Core.Tests;

public class PartNameParserTests
{
    [Theory]
    [InlineData("2835.01.008 Отбортовка", "2835.01.008", "Отбортовка")]
    [InlineData("2835.01.008", "2835.01.008", "")]
    [InlineData("Крышка", "", "Крышка")]
    [InlineData("", "", "")]
    [InlineData("  6516.01.03.001   Планка боковая  ", "6516.01.03.001", "Планка боковая")]
    public void Split_SeparatesNumberAndTitle(string raw, string expectedNumber, string expectedTitle)
    {
        var (number, title) = PartNameParser.Split(raw);
        Assert.Equal(expectedNumber, number);
        Assert.Equal(expectedTitle, title);
    }

    [Fact]
    public void Part_DisplayName_JoinsNumberAndTitle()
    {
        var part = new Part { Number = "2835.01.008", Title = "Отбортовка" };
        Assert.Equal("2835.01.008 Отбортовка", part.DisplayName);
    }

    [Fact]
    public void Part_DisplayName_SkipsEmptyTitle()
    {
        var part = new Part { Number = "2835.01.008", Title = "" };
        Assert.Equal("2835.01.008", part.DisplayName);
    }
}
