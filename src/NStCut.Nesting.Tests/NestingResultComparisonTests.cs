using NStCut.Core.Models;
using NStCut.Core.Nesting;

namespace NStCut.Nesting.Tests;

public class NestingResultComparisonTests
{
    [Fact]
    public void IsBetter_FewerUnplacedWins()
    {
        var better = new NestingResult();
        better.Unplaced.Add(new Part());

        var worse = new NestingResult();
        worse.Unplaced.Add(new Part());
        worse.Unplaced.Add(new Part());

        Assert.True(NestingResultComparison.IsBetter(better, worse));
        Assert.False(NestingResultComparison.IsBetter(worse, better));
    }

    [Fact]
    public void IsBetter_SameUnplacedFewerSheetsWins()
    {
        var better = new NestingResult();
        better.Sheets.Add(new SheetInstance());

        var worse = new NestingResult();
        worse.Sheets.Add(new SheetInstance());
        worse.Sheets.Add(new SheetInstance());

        Assert.True(NestingResultComparison.IsBetter(better, worse));
        Assert.False(NestingResultComparison.IsBetter(worse, better));
    }

    [Fact]
    public void IsBetter_SameUnplacedAndSheetsHigherUtilizationWins()
    {
        var better = new NestingResult { TotalUtilizationPercent = 80.0 };
        better.Sheets.Add(new SheetInstance());

        var worse = new NestingResult { TotalUtilizationPercent = 70.0 };
        worse.Sheets.Add(new SheetInstance());

        Assert.True(NestingResultComparison.IsBetter(better, worse));
        Assert.False(NestingResultComparison.IsBetter(worse, better));
    }

    [Fact]
    public void IsBetter_IdenticalIsNotBetter()
    {
        var a = new NestingResult { TotalUtilizationPercent = 50.0 };
        a.Sheets.Add(new SheetInstance());

        var b = new NestingResult { TotalUtilizationPercent = 50.0 };
        b.Sheets.Add(new SheetInstance());

        Assert.False(NestingResultComparison.IsBetter(a, b));
        Assert.False(NestingResultComparison.IsBetter(b, a));
    }
}
