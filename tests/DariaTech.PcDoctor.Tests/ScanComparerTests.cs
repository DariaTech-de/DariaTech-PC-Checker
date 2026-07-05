using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class ScanComparerTests
{
    private static CheckResult R(string area, string label, Severity sev)
        => new(area, label, sev.ToString(), sev);

    private static readonly DateTime T0 = new(2026, 7, 5, 10, 0, 0);
    private static readonly DateTime T1 = new(2026, 7, 5, 11, 0, 0);

    [Fact]
    public void Compare_FixedCriticalArea_CountsAsImproved()
    {
        var before = new List<CheckResult>
        {
            R("Datenträger", "C:", Severity.Critical),
            R("Netzwerk", "Internet", Severity.Ok),
        };
        var after = new List<CheckResult>
        {
            R("Datenträger", "C:", Severity.Ok),
            R("Netzwerk", "Internet", Severity.Ok),
        };

        var c = ScanComparer.Compare(before, T0, after, T1);

        Assert.True(c.HasChanges);
        Assert.Single(c.Improved);
        Assert.Equal("Datenträger", c.Improved[0].Area);
        Assert.Empty(c.Worsened);
        Assert.Equal(1, c.CriticalBefore);
        Assert.Equal(0, c.CriticalAfter);
        Assert.True(c.ScoreDelta > 0);
        Assert.Equal("Verbesserung", c.Trend);
    }

    [Fact]
    public void Compare_NewProblemArea_CountsAsWorsened()
    {
        var before = new List<CheckResult> { R("Netzwerk", "Internet", Severity.Ok) };
        var after = new List<CheckResult> { R("Netzwerk", "Internet", Severity.Warning) };

        var c = ScanComparer.Compare(before, T0, after, T1);

        Assert.Single(c.Worsened);
        Assert.Empty(c.Improved);
        Assert.True(c.ScoreDelta < 0);
        Assert.Equal("Verschlechterung", c.Trend);
    }

    [Fact]
    public void Compare_Identical_HasNoChanges()
    {
        var results = new List<CheckResult>
        {
            R("Datenträger", "C:", Severity.Ok),
            R("Netzwerk", "Internet", Severity.Warning),
        };

        var c = ScanComparer.Compare(results, T0, results, T1);

        Assert.False(c.HasChanges);
        Assert.Empty(c.Improved);
        Assert.Empty(c.Worsened);
        Assert.Equal(0, c.ScoreDelta);
        Assert.Equal("unverändert", c.Trend);
    }

    [Fact]
    public void Compare_AreaOnlyInAfter_IsIgnored()
    {
        var before = new List<CheckResult> { R("Netzwerk", "Internet", Severity.Ok) };
        var after = new List<CheckResult>
        {
            R("Netzwerk", "Internet", Severity.Ok),
            R("Akku", "Verschleiß", Severity.Critical),   // neuer Bereich -> nicht als "verschlechtert"
        };

        var c = ScanComparer.Compare(before, T0, after, T1);

        Assert.Empty(c.Worsened);
        Assert.Empty(c.Improved);
    }
}
