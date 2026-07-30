using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

/// <summary>
/// Diese Bewertung entscheidet, ob vor einer Reparatur gewarnt wird. Ein Fehler
/// hier kann Kundendaten kosten: Wird ein verschlüsseltes Laufwerk ohne
/// Wiederherstellungsschlüssel nicht als kritisch gemeldet, kann ein Eingriff
/// den Zugang zu allen Daten dauerhaft verhindern.
/// </summary>
public class BitLockerRulesTests
{
    private static BitLockerVolume Volume(
        int protection = BitLockerRules.ProtectionOn,
        int conversion = BitLockerRules.FullyEncrypted,
        bool hasKey = true)
        => new("C:", protection, conversion, hasKey);

    [Fact]
    public void EncryptedWithoutRecoveryKey_IsCritical()
    {
        var volume = Volume(hasKey: false);
        Assert.Equal(Severity.Critical, BitLockerRules.SeverityFor(volume));
        Assert.Contains("ACHTUNG", BitLockerRules.AdviceFor(volume));
    }

    [Fact]
    public void EncryptedWithRecoveryKey_IsNotCritical()
    {
        var volume = Volume(hasKey: true);
        Assert.NotEqual(Severity.Critical, BitLockerRules.SeverityFor(volume));
        Assert.Contains("griffbereit", BitLockerRules.AdviceFor(volume));
    }

    [Fact]
    public void UnencryptedDrive_NeedsNoKeyAndIsNotCritical()
    {
        var volume = Volume(BitLockerRules.ProtectionOff, BitLockerRules.FullyDecrypted, hasKey: false);
        Assert.False(BitLockerRules.IsEncrypted(volume));
        Assert.Equal(Severity.Info, BitLockerRules.SeverityFor(volume));
        Assert.Null(BitLockerRules.AdviceFor(volume));
    }

    [Theory]
    [InlineData(BitLockerRules.EncryptionInProgress)]
    [InlineData(BitLockerRules.DecryptionInProgress)]
    public void ConversionInProgress_WarnsAgainstRepairs(int conversion)
    {
        var volume = Volume(conversion: conversion, hasKey: true);
        Assert.Equal(Severity.Warning, BitLockerRules.SeverityFor(volume));
        Assert.Contains("läuft gerade", BitLockerRules.AdviceFor(volume));
    }

    [Fact]
    public void EncryptionInProgressWithoutKey_StaysCritical()
    {
        // Fehlender Schlüssel wiegt schwerer als der laufende Vorgang.
        var volume = Volume(conversion: BitLockerRules.EncryptionInProgress, hasKey: false);
        Assert.Equal(Severity.Critical, BitLockerRules.SeverityFor(volume));
    }

    [Theory]
    [InlineData(BitLockerRules.FullyEncrypted, true)]
    [InlineData(BitLockerRules.EncryptionInProgress, true)]
    [InlineData(BitLockerRules.EncryptionPaused, true)]
    [InlineData(BitLockerRules.FullyDecrypted, false)]
    public void IsEncrypted_CoversPartialStates(int conversion, bool expected)
        => Assert.Equal(expected,
            BitLockerRules.IsEncrypted(new BitLockerVolume("D:", BitLockerRules.ProtectionOff, conversion, true)));

    [Fact]
    public void StatusText_DistinguishesActiveAndSuspendedProtection()
    {
        Assert.Contains("Schutz aktiv", BitLockerRules.StatusText(Volume()));
        Assert.Contains("ausgesetzt", BitLockerRules.StatusText(
            Volume(BitLockerRules.ProtectionOff, BitLockerRules.FullyEncrypted)));
    }
}

public class RestoreProtectionRulesTests
{
    private static readonly DateTime Now = new(2026, 7, 30, 12, 0, 0);

    [Fact]
    public void ProtectionDisabled_IsWarning()
    {
        var result = RestoreProtectionRules.Evaluate(Array.Empty<DateTime>(), Now, protectionDisabled: true);
        Assert.Equal(Severity.Warning, result.Severity);
        Assert.Contains("abgeschaltet", result.Summary);
    }

    [Fact]
    public void NoRestorePoints_IsWarning()
    {
        var result = RestoreProtectionRules.Evaluate(Array.Empty<DateTime>(), Now);
        Assert.Equal(Severity.Warning, result.Severity);
        Assert.Contains("keine Wiederherstellungspunkte", result.Summary);
    }

    [Fact]
    public void RecentRestorePoint_IsOk()
    {
        var result = RestoreProtectionRules.Evaluate(new[] { Now.AddDays(-3) }, Now);
        Assert.Equal(Severity.Ok, result.Severity);
        Assert.Contains("27.07.2026", result.Summary);
    }

    [Fact]
    public void StaleRestorePoint_IsInfoWithAge()
    {
        var result = RestoreProtectionRules.Evaluate(new[] { Now.AddDays(-45) }, Now);
        Assert.Equal(Severity.Info, result.Severity);
        Assert.Contains("45 Tage", result.Summary);
    }

    [Fact]
    public void NewestPointDecides_NotTheOldest()
    {
        var points = new[] { Now.AddDays(-200), Now.AddDays(-1), Now.AddDays(-90) };
        var result = RestoreProtectionRules.Evaluate(points, Now);
        Assert.Equal(Severity.Ok, result.Severity);
        Assert.Contains("3 Punkt(e)", result.Summary);
    }

    [Fact]
    public void UnknownProtectionState_DoesNotClaimItIsDisabled()
    {
        // protectionDisabled = null -> es wird nur nach den Punkten bewertet.
        var result = RestoreProtectionRules.Evaluate(new[] { Now.AddDays(-2) }, Now, protectionDisabled: null);
        Assert.Equal(Severity.Ok, result.Severity);
        Assert.DoesNotContain("abgeschaltet", result.Summary);
    }

    [Fact]
    public void ExplicitlyEnabledProtectionWithoutPoints_StillWarns()
    {
        var result = RestoreProtectionRules.Evaluate(Array.Empty<DateTime>(), Now, protectionDisabled: false);
        Assert.Equal(Severity.Warning, result.Severity);
    }
}
