using DariaTech.PcDoctor.Checks;
using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class UpdateCorrelatorTests
{
    private static readonly DateTime Base = new(2026, 7, 1);

    private static InstalledUpdate U(string id, int dayOffset)
        => new(id, Base.AddDays(dayOffset));

    private static CrashGroup G(int count, int firstDayOffset)
        => new("explorer.exe", "x.dll", count, Base.AddDays(firstDayOffset), Base.AddDays(firstDayOffset + 1));

    [Fact]
    public void FindSuspect_UpdateShortlyBeforeCrashes_IsFlagged()
    {
        var updates = new[] { U("KB5001", 1), U("KB5002", 8) };   // KB5002 am 09.07.
        var onset = Base.AddDays(9);                              // Abstürze ab 10.07.

        var suspect = UpdateCorrelator.FindSuspect(updates, onset);

        Assert.NotNull(suspect);
        Assert.Equal("KB5002", suspect!.HotFixId);
    }

    [Fact]
    public void FindSuspect_PicksMostRecentUpdateInWindow()
    {
        var updates = new[] { U("KB-alt", 8), U("KB-neu", 9) };
        var suspect = UpdateCorrelator.FindSuspect(updates, Base.AddDays(10));
        Assert.Equal("KB-neu", suspect!.HotFixId);
    }

    [Fact]
    public void FindSuspect_UpdateLongBeforeCrashes_IsNotFlagged()
    {
        var updates = new[] { U("KB5001", 0) };       // Update am 01.07.
        var suspect = UpdateCorrelator.FindSuspect(updates, Base.AddDays(20));
        Assert.Null(suspect);
    }

    [Fact]
    public void FindSuspect_UpdateAfterCrashes_IsNotFlagged()
    {
        var updates = new[] { U("KB5001", 12) };      // Update NACH Absturzbeginn
        var suspect = UpdateCorrelator.FindSuspect(updates, Base.AddDays(10));
        Assert.Null(suspect);
    }

    [Fact]
    public void FindSuspect_WithoutCrashOnset_ReturnsNull()
        => Assert.Null(UpdateCorrelator.FindSuspect(new[] { U("KB5001", 1) }, null));

    [Fact]
    public void CrashOnset_UsesEarliestGroupAboveWarnThreshold()
    {
        var groups = new[]
        {
            G(count: 1, firstDayOffset: 2),    // Einzelfall -> zählt nicht
            G(count: 5, firstDayOffset: 9),
            G(count: 4, firstDayOffset: 11)
        };

        Assert.Equal(Base.AddDays(9), UpdateCorrelator.CrashOnset(groups));
    }

    [Fact]
    public void CrashOnset_OnlyIsolatedCrashes_ReturnsNull()
        => Assert.Null(UpdateCorrelator.CrashOnset(new[] { G(1, 3), G(2, 5) }));

    // Bewusst ein eindeutiges Datum (Tag 15 kann kein Monat sein): so ist das
    // Ergebnis unabhängig von der Sprache/Kultur des ausführenden Systems.
    [Theory]
    [InlineData("7/15/2026")]
    [InlineData("15.07.2026")]
    [InlineData("20260715")]
    public void ParseInstalledOn_HandlesCommonFormats(string value)
    {
        var parsed = UpdateStabilityCheck.ParseInstalledOn(value);
        Assert.NotNull(parsed);
        Assert.Equal(new DateTime(2026, 7, 15), parsed!.Value.Date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein Datum")]
    public void ParseInstalledOn_InvalidInput_ReturnsNull(string? value)
        => Assert.Null(UpdateStabilityCheck.ParseInstalledOn(value));
}
