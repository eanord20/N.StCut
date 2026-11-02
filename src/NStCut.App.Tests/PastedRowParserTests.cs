using NStCut.App.Services;

namespace NStCut.App.Tests;

public class PastedRowParserTests
{
    [Fact]
    public void Parse_SingleColumn_SplitsViaPartNameParser()
    {
        var (number, title, qty) = PastedRowParser.Parse(new[] { "2835.01.008 Отбортовка" });

        Assert.Equal("2835.01.008", number);
        Assert.Equal("Отбортовка", title);
        Assert.Equal(1, qty);
    }

    [Fact]
    public void Parse_TwoColumns_AreNumberAndTitle()
    {
        var (number, title, qty) = PastedRowParser.Parse(new[] { "111", "Крышка" });

        Assert.Equal("111", number);
        Assert.Equal("Крышка", title);
        Assert.Equal(1, qty);
    }

    [Fact]
    public void Parse_ThreeColumns_AreNumberTitleAndQuantity()
    {
        var (number, title, qty) = PastedRowParser.Parse(new[] { "2835.01.008", "Отбортовка", "5" });

        Assert.Equal("2835.01.008", number);
        Assert.Equal("Отбортовка", title);
        Assert.Equal(5, qty);
    }

    [Fact]
    public void Parse_ThreeColumns_InvalidQuantity_DefaultsToOne()
    {
        var (_, _, qty) = PastedRowParser.Parse(new[] { "2835.01.008", "Отбортовка", "n/a" });

        Assert.Equal(1, qty);
    }

    [Fact]
    public void Parse_EmptyCells_ReturnsEmptyDefaults()
    {
        var (number, title, qty) = PastedRowParser.Parse(Array.Empty<string>());

        Assert.Equal(string.Empty, number);
        Assert.Equal(string.Empty, title);
        Assert.Equal(1, qty);
    }
}
