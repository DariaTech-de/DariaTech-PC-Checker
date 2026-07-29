using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

/// <summary>
/// Diese Zuordnung entscheidet, ob eine gefundene Bedrohung als „erledigt“ oder
/// „noch aktiv“ gilt. Ein Fehler hier wäre gefährlich: Der Kunde bekäme eine
/// Entwarnung, obwohl der Schädling weiterläuft.
/// </summary>
public class ThreatStatusMapperTests
{
    private static ThreatRecord Threat(int statusId, int severityId = 5)
        => new("Trojan:Win32/Testfall", severityId, statusId, new DateTime(2026, 7, 20, 9, 30, 0), @"C:\Temp\x.exe");

    [Theory]
    [InlineData(ThreatStatusMapper.StatusCleaned)]
    [InlineData(ThreatStatusMapper.StatusQuarantined)]
    [InlineData(ThreatStatusMapper.StatusRemoved)]
    [InlineData(ThreatStatusMapper.StatusBlocked)]
    public void NeedsAction_IsFalse_WhenThreatWasHandled(int statusId)
        => Assert.False(ThreatStatusMapper.NeedsAction(statusId));

    [Theory]
    [InlineData(ThreatStatusMapper.StatusDetected)]          // nur erkannt, nichts getan
    [InlineData(ThreatStatusMapper.StatusAllowed)]           // vom Benutzer zugelassen -> laeuft weiter!
    [InlineData(ThreatStatusMapper.StatusQuarantineFailed)]
    [InlineData(ThreatStatusMapper.StatusRemoveFailed)]
    [InlineData(ThreatStatusMapper.StatusAbandoned)]
    [InlineData(ThreatStatusMapper.StatusBlockFailed)]
    [InlineData(ThreatStatusMapper.StatusUnknown)]
    public void NeedsAction_IsTrue_WhenThreatIsNotSafelyHandled(int statusId)
        => Assert.True(ThreatStatusMapper.NeedsAction(statusId));

    [Fact]
    public void SeverityFor_UnhandledThreat_IsCritical()
        => Assert.Equal(Severity.Critical,
            ThreatStatusMapper.SeverityFor(Threat(ThreatStatusMapper.StatusDetected)));

    [Fact]
    public void SeverityFor_AllowedThreat_IsCritical()
        => Assert.Equal(Severity.Critical,
            ThreatStatusMapper.SeverityFor(Threat(ThreatStatusMapper.StatusAllowed)));

    [Fact]
    public void SeverityFor_HandledButSevereThreat_IsWarning()
        => Assert.Equal(Severity.Warning,
            ThreatStatusMapper.SeverityFor(Threat(ThreatStatusMapper.StatusQuarantined, severityId: 5)));

    [Fact]
    public void SeverityFor_HandledLowSeverity_IsInfo()
        => Assert.Equal(Severity.Info,
            ThreatStatusMapper.SeverityFor(Threat(ThreatStatusMapper.StatusCleaned, severityId: 1)));

    [Fact]
    public void Overall_TakesWorstCase()
    {
        var threats = new[]
        {
            Threat(ThreatStatusMapper.StatusCleaned, 1),
            Threat(ThreatStatusMapper.StatusQuarantined, 5),
            Threat(ThreatStatusMapper.StatusDetected, 4)
        };
        Assert.Equal(Severity.Critical, ThreatStatusMapper.Overall(threats));
    }

    [Fact]
    public void Overall_WithoutThreats_IsOk()
        => Assert.Equal(Severity.Ok, ThreatStatusMapper.Overall(Array.Empty<ThreatRecord>()));

    [Theory]
    [InlineData(1, "niedrig")]
    [InlineData(2, "mittel")]
    [InlineData(4, "hoch")]
    [InlineData(5, "schwerwiegend")]
    [InlineData(99, "unbekannt")]
    public void SeverityText_IsGerman(int id, string expected)
        => Assert.Equal(expected, ThreatStatusMapper.SeverityText(id));

    [Fact]
    public void StatusText_CoversKnownAndUnknownCodes()
    {
        Assert.Equal("in Quarantäne", ThreatStatusMapper.StatusText(ThreatStatusMapper.StatusQuarantined));
        Assert.Contains("zugelassen", ThreatStatusMapper.StatusText(ThreatStatusMapper.StatusAllowed));
        Assert.Contains("42", ThreatStatusMapper.StatusText(42));
    }

    [Fact]
    public void Describe_ContainsSeverityStatusAndDate()
    {
        var text = ThreatStatusMapper.Describe(Threat(ThreatStatusMapper.StatusQuarantined));
        Assert.Contains("schwerwiegend", text);
        Assert.Contains("Quarantäne", text);
        Assert.Contains("20.07.2026", text);
    }

    [Fact]
    public void Describe_WithoutDate_OmitsTimestamp()
    {
        var text = ThreatStatusMapper.Describe(new ThreatRecord("X", 2, ThreatStatusMapper.StatusCleaned, null, null));
        Assert.DoesNotContain("·  ", text);
        Assert.Contains("bereinigt", text);
    }
}
