using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class AppCrashAnalyzerTests
{
    private static readonly DateTime Day = new(2026, 7, 9, 10, 0, 0);

    private static CrashEvent C(string app, string module, int dayOffset, int hour = 10)
        => new(app, module, Day.AddDays(dayOffset).AddHours(hour - 10));

    [Fact]
    public void Group_CountsPerAppAndModule_AndTracksTimespan()
    {
        var crashes = new[]
        {
            C("explorer.exe", "windows.storage.dll", 0),
            C("explorer.exe", "windows.storage.dll", 1),
            C("explorer.exe", "windows.storage.dll", 2, 15),
            C("msedge.exe", "", 1)
        };

        var groups = AppCrashAnalyzer.Group(crashes);

        Assert.Equal(2, groups.Count);
        var top = groups[0];                       // häufigste zuerst
        Assert.Equal("explorer.exe", top.App);
        Assert.Equal("windows.storage.dll", top.Module);
        Assert.Equal(3, top.Count);
        Assert.Equal(Day, top.First);
        Assert.Equal(Day.AddDays(2).AddHours(5), top.Last);
    }

    [Fact]
    public void Group_SeparatesDifferentModulesOfSameApp()
    {
        var groups = AppCrashAnalyzer.Group(new[]
        {
            C("explorer.exe", "a.dll", 0),
            C("explorer.exe", "b.dll", 0)
        });

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void Group_IgnoresEntriesWithoutAppName()
        => Assert.Empty(AppCrashAnalyzer.Group(new[] { C("", "x.dll", 0), C("   ", "", 0) }));

    [Theory]
    [InlineData(1, Severity.Info)]
    [InlineData(3, Severity.Warning)]
    [InlineData(10, Severity.Critical)]
    [InlineData(25, Severity.Critical)]
    public void SeverityFor_FollowsCountThresholds(int count, Severity expected)
    {
        var group = new CrashGroup("app.exe", "m.dll", count, Day, Day);
        Assert.Equal(expected, AppCrashAnalyzer.SeverityFor(group));
    }

    [Theory]
    [InlineData("SearchHost.exe", "Windows-Suche")]
    [InlineData("explorer.exe", "Explorer")]
    [InlineData("TextInputHost.exe", "Texteingabe")]
    [InlineData("msedge.exe", "Browser")]
    public void TipFor_NamesTheMatchingRepair(string app, string expectedFragment)
    {
        var tip = AppCrashAnalyzer.TipFor(new CrashGroup(app, "", 5, Day, Day));
        Assert.Contains(expectedFragment, tip);
    }

    [Fact]
    public void TipFor_UnknownApp_StillGivesGuidance()
    {
        var tip = AppCrashAnalyzer.TipFor(new CrashGroup("irgendwas.exe", "", 5, Day, Day));
        Assert.Contains("Systemdateien reparieren", tip);
    }

    [Fact]
    public void Describe_ContainsCountAndTimeframe()
    {
        var text = AppCrashAnalyzer.Describe(new CrashGroup("a.exe", "", 14, Day, Day.AddDays(2)));
        Assert.Contains("14x", text);
        Assert.Contains("09.07.", text);
    }
}
