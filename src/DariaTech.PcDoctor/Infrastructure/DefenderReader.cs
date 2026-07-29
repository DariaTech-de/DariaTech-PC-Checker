using System.Management;
using DariaTech.PcDoctor.Core.Security;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>Schutzstatus von Microsoft Defender (für die Befall-Indikatoren).</summary>
/// <param name="Available">Konnte der Defender-Status überhaupt gelesen werden?</param>
/// <param name="RealTimeProtection">Echtzeitschutz aktiv?</param>
/// <param name="TamperProtection">Manipulationsschutz aktiv?</param>
/// <param name="AntivirusEnabled">Virenschutz grundsätzlich aktiv?</param>
/// <param name="SignatureAgeDays">Alter der Virensignaturen in Tagen (null = unbekannt).</param>
public sealed record DefenderStatus(
    bool Available,
    bool RealTimeProtection,
    bool TamperProtection,
    bool AntivirusEnabled,
    int? SignatureAgeDays);

/// <summary>
/// Liest Microsoft-Defender-Informationen über WMI
/// (<c>root\Microsoft\Windows\Defender</c>): erkannte Bedrohungen, Schutzstatus
/// und eingetragene Ausnahmen.
///
/// Bewusst über WMI statt über Text-Ausgaben von PowerShell – damit gibt es keine
/// sprach- oder formatabhängige Auswertung, die auf Kundengeräten überraschend
/// bricht. Alle Zugriffe fangen Fehler ab und liefern dann „nicht verfügbar“.
/// </summary>
public static class DefenderReader
{
    private const string Scope = @"\\.\root\Microsoft\Windows\Defender";

    /// <summary>Erkannte Bedrohungen aus der Defender-Historie, neueste zuerst.</summary>
    public static IReadOnlyList<ThreatRecord> ReadThreats(CancellationToken ct = default)
    {
        var threats = new List<ThreatRecord>();
        try
        {
            // Bedrohungsnamen und Schweregrade (ThreatID -> Name/Severity).
            var catalog = new Dictionary<string, (string Name, int Severity)>();
            foreach (var obj in Query("SELECT ThreatID, ThreatName, SeverityID FROM MSFT_MpThreat", ct))
            {
                using (obj)
                {
                    var id = obj["ThreatID"]?.ToString();
                    if (string.IsNullOrEmpty(id)) continue;
                    catalog[id] = (
                        obj["ThreatName"]?.ToString() ?? "Unbekannte Bedrohung",
                        ToInt(obj["SeverityID"]));
                }
            }

            // Konkrete Erkennungen (mit Status und Zeitpunkt).
            foreach (var obj in Query(
                "SELECT ThreatID, InitialDetectionTime, ThreatStatusID, Resources FROM MSFT_MpThreatDetection", ct))
            {
                using (obj)
                {
                    var id = obj["ThreatID"]?.ToString() ?? string.Empty;
                    var known = catalog.TryGetValue(id, out var info);

                    threats.Add(new ThreatRecord(
                        known ? info.Name : "Unbekannte Bedrohung",
                        known ? info.Severity : 0,
                        ToInt(obj["ThreatStatusID"]),
                        ToDate(obj["InitialDetectionTime"]),
                        FirstResource(obj["Resources"])));
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Defender nicht vorhanden / kein Zugriff */ }

        return threats
            .OrderByDescending(t => t.DetectedAt ?? DateTime.MinValue)
            .ToList();
    }

    /// <summary>Schutzstatus von Defender.</summary>
    public static DefenderStatus ReadStatus(CancellationToken ct = default)
    {
        try
        {
            foreach (var obj in Query(
                "SELECT RealTimeProtectionEnabled, IsTamperProtected, AntivirusEnabled, " +
                "AntivirusSignatureAge FROM MSFT_MpComputerStatus", ct))
            {
                using (obj)
                {
                    var age = obj["AntivirusSignatureAge"] is null ? (int?)null : ToInt(obj["AntivirusSignatureAge"]);
                    return new DefenderStatus(
                        Available: true,
                        RealTimeProtection: obj["RealTimeProtectionEnabled"] is bool rtp && rtp,
                        TamperProtection: obj["IsTamperProtected"] is bool tp && tp,
                        AntivirusEnabled: obj["AntivirusEnabled"] is bool av && av,
                        SignatureAgeDays: age);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* nicht lesbar */ }

        return new DefenderStatus(false, false, false, false, null);
    }

    /// <summary>
    /// Eingetragene Scan-Ausnahmen (Pfade, Erweiterungen, Prozesse). Schadsoftware
    /// trägt gern eigene Ausnahmen ein, damit sie nicht gefunden wird – deshalb
    /// gehören sie in den Bericht.
    /// </summary>
    public static IReadOnlyList<string> ReadExclusions(CancellationToken ct = default)
    {
        var exclusions = new List<string>();
        try
        {
            foreach (var obj in Query(
                "SELECT ExclusionPath, ExclusionExtension, ExclusionProcess FROM MSFT_MpPreference", ct))
            {
                using (obj)
                {
                    AddAll(exclusions, obj["ExclusionPath"], "Pfad");
                    AddAll(exclusions, obj["ExclusionExtension"], "Dateityp");
                    AddAll(exclusions, obj["ExclusionProcess"], "Prozess");
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* nicht lesbar */ }

        return exclusions;
    }

    private static IEnumerable<ManagementBaseObject> Query(string wql, CancellationToken ct)
    {
        var searcher = new ManagementObjectSearcher(new ManagementScope(Scope), new ObjectQuery(wql));
        using (searcher)
        {
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                ct.ThrowIfCancellationRequested();
                yield return obj;
            }
        }
    }

    private static void AddAll(List<string> target, object? value, string kind)
    {
        if (value is not string[] items) return;
        foreach (var item in items)
            if (!string.IsNullOrWhiteSpace(item))
                target.Add($"{kind}: {item}");
    }

    private static string? FirstResource(object? value)
    {
        if (value is string[] items && items.Length > 0) return items[0];
        return value?.ToString();
    }

    private static int ToInt(object? value)
    {
        try { return value is null ? 0 : Convert.ToInt32(value); }
        catch { return 0; }
    }

    private static DateTime? ToDate(object? value)
    {
        if (value is null) return null;
        try { return ManagementDateTimeConverter.ToDateTime(value.ToString()); }
        catch { return null; }
    }
}
