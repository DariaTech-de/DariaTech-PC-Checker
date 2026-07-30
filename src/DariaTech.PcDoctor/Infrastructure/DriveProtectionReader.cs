using System.Management;
using DariaTech.PcDoctor.Core.Security;
using Microsoft.Win32;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>
/// Liest BitLocker-Status und vorhandene Wiederherstellungspunkte über WMI.
/// Alle Zugriffe fangen Fehler ab: Auf Windows-Home-Systemen fehlt der
/// BitLocker-Namespace komplett – das ist kein Fehler, sondern der Normalfall
/// und muss sauber als „nicht verfügbar“ gemeldet werden.
/// </summary>
public static class DriveProtectionReader
{
    private const string BitLockerScope = @"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption";

    /// <summary>Schlüsselschutz-Typ „Wiederherstellungskennwort“ (48-stelliger Zahlencode).</summary>
    private const uint RecoveryPasswordProtector = 3;

    /// <summary>
    /// BitLocker-Zustand aller Laufwerke. Leere Liste = BitLocker nicht verfügbar
    /// (z. B. Windows Home) oder kein Zugriff.
    /// </summary>
    public static IReadOnlyList<BitLockerVolume> ReadBitLocker(CancellationToken ct = default)
    {
        var volumes = new List<BitLockerVolume>();
        try
        {
            var scope = new ManagementScope(BitLockerScope);
            var query = new ObjectQuery(
                "SELECT DriveLetter, ProtectionStatus, ConversionStatus FROM Win32_EncryptableVolume");
            using var searcher = new ManagementObjectSearcher(scope, query);

            foreach (ManagementBaseObject item in searcher.Get())
            {
                ct.ThrowIfCancellationRequested();
                using var volume = (ManagementObject)item;

                var letter = volume["DriveLetter"]?.ToString();
                if (string.IsNullOrWhiteSpace(letter)) continue;

                volumes.Add(new BitLockerVolume(
                    letter,
                    ToInt(volume["ProtectionStatus"]),
                    ToInt(volume["ConversionStatus"]),
                    HasRecoveryPassword(volume)));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* BitLocker nicht verfügbar */ }

        return volumes;
    }

    /// <summary>
    /// Prüft, ob für das Laufwerk ein Wiederherstellungskennwort hinterlegt ist.
    /// Im Zweifel wird <c>false</c> geliefert – lieber einmal zu viel warnen als
    /// fälschlich Sicherheit vorgaukeln.
    /// </summary>
    private static bool HasRecoveryPassword(ManagementObject volume)
    {
        try
        {
            var inParams = volume.GetMethodParameters("GetKeyProtectors");
            inParams["KeyProtectorType"] = RecoveryPasswordProtector;

            using var outParams = volume.InvokeMethod("GetKeyProtectors", inParams, null);
            if (outParams is null) return false;
            if (ToInt(outParams["ReturnValue"]) != 0) return false;

            return outParams["VolumeKeyProtectorID"] is string[] { Length: > 0 };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Zeitpunkte vorhandener Wiederherstellungspunkte (neueste zuerst).</summary>
    public static IReadOnlyList<DateTime> ReadRestorePoints(CancellationToken ct = default)
    {
        var points = new List<DateTime>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\default");
            using var searcher = new ManagementObjectSearcher(
                scope, new ObjectQuery("SELECT CreationTime FROM SystemRestore"));

            foreach (ManagementBaseObject item in searcher.Get())
            {
                ct.ThrowIfCancellationRequested();
                using (item)
                {
                    var raw = item["CreationTime"]?.ToString();
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    try { points.Add(ManagementDateTimeConverter.ToDateTime(raw)); }
                    catch { /* unlesbarer Zeitstempel */ }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Systemschutz aus oder kein Zugriff */ }

        return points.OrderByDescending(p => p).ToList();
    }

    /// <summary>
    /// True, wenn der Systemschutz nachweislich abgeschaltet ist; null, wenn sich
    /// das nicht sicher feststellen lässt (dann nicht behaupten, sondern offen lassen).
    /// </summary>
    public static bool? IsSystemRestoreDisabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore");
            if (key is null) return null;

            if (key.GetValue("DisableSR") is int disabled) return disabled == 1;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static int ToInt(object? value)
    {
        try { return value is null ? -1 : Convert.ToInt32(value); }
        catch { return -1; }
    }
}
