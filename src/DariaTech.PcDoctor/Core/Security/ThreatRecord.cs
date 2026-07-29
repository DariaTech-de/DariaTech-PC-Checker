namespace DariaTech.PcDoctor.Core.Security;

/// <summary>
/// Eine von Microsoft Defender erkannte Bedrohung (aus der Erkennungshistorie).
/// </summary>
/// <param name="Name">Bedrohungsname, z. B. „Trojan:Win32/Wacatac.B!ml“.</param>
/// <param name="SeverityId">Defender-Schweregrad (1=niedrig, 2=mittel, 4=hoch, 5=schwer).</param>
/// <param name="StatusId">Defender-Status der Erkennung (siehe <see cref="ThreatStatusMapper"/>).</param>
/// <param name="DetectedAt">Zeitpunkt der ersten Erkennung.</param>
/// <param name="Resource">Betroffene Datei/Ressource, falls bekannt.</param>
public sealed record ThreatRecord(
    string Name,
    int SeverityId,
    int StatusId,
    DateTime? DetectedAt,
    string? Resource);

/// <summary>
/// Übersetzt die numerischen Defender-Codes in verständlichen Text und in unsere
/// Ampel. Rein funktional und damit vollständig testbar – wichtig, weil an dieser
/// Zuordnung hängt, ob eine Bedrohung als „erledigt“ oder „noch aktiv“ gilt.
///
/// Status-Codes laut Microsoft (MSFT_MpThreatDetection.ThreatStatusID).
/// </summary>
public static class ThreatStatusMapper
{
    public const int StatusUnknown = 0;
    public const int StatusDetected = 1;
    public const int StatusCleaned = 2;
    public const int StatusQuarantined = 3;
    public const int StatusRemoved = 4;
    public const int StatusAllowed = 5;
    public const int StatusBlocked = 6;
    public const int StatusQuarantineFailed = 102;
    public const int StatusRemoveFailed = 103;
    public const int StatusAllowFailed = 104;
    public const int StatusAbandoned = 105;
    public const int StatusBlockFailed = 107;

    /// <summary>Klartext für den Erkennungsstatus.</summary>
    public static string StatusText(int statusId) => statusId switch
    {
        StatusUnknown => "Status unbekannt",
        StatusDetected => "erkannt, noch nicht behandelt",
        StatusCleaned => "bereinigt",
        StatusQuarantined => "in Quarantäne",
        StatusRemoved => "entfernt",
        StatusAllowed => "vom Benutzer zugelassen",
        StatusBlocked => "blockiert",
        StatusQuarantineFailed => "Quarantäne fehlgeschlagen",
        StatusRemoveFailed => "Entfernen fehlgeschlagen",
        StatusAllowFailed => "Zulassen fehlgeschlagen",
        StatusAbandoned => "Behandlung abgebrochen",
        StatusBlockFailed => "Blockieren fehlgeschlagen",
        _ => $"Status {statusId}"
    };

    /// <summary>
    /// True, wenn die Bedrohung noch Handlungsbedarf hat – also NICHT sicher
    /// beseitigt ist. „Zugelassen“ zählt bewusst dazu: Ein zugelassener Trojaner
    /// ist weiterhin aktiv und muss dem Techniker auffallen.
    /// </summary>
    public static bool NeedsAction(int statusId) => statusId switch
    {
        StatusCleaned => false,
        StatusQuarantined => false,
        StatusRemoved => false,
        StatusBlocked => false,
        _ => true
    };

    /// <summary>Klartext für den Schweregrad.</summary>
    public static string SeverityText(int severityId) => severityId switch
    {
        1 => "niedrig",
        2 => "mittel",
        4 => "hoch",
        5 => "schwerwiegend",
        _ => "unbekannt"
    };

    /// <summary>
    /// Ampel für eine Bedrohung: Offener Handlungsbedarf ist immer kritisch.
    /// Bereits beseitigte Funde sind ein Hinweis (dokumentieren, nicht alarmieren)
    /// – außer bei hohem Schweregrad, dann als Warnung, weil der Befall zeigt,
    /// dass etwas durchgekommen ist.
    /// </summary>
    public static Severity SeverityFor(ThreatRecord threat)
    {
        if (NeedsAction(threat.StatusId)) return Severity.Critical;
        return threat.SeverityId >= 4 ? Severity.Warning : Severity.Info;
    }

    /// <summary>Zusammenfassende Ampel über alle Funde.</summary>
    public static Severity Overall(IEnumerable<ThreatRecord> threats)
    {
        var worst = Severity.Ok;
        foreach (var t in threats)
        {
            var s = SeverityFor(t);
            if (s > worst) worst = s;
        }
        return worst;
    }

    /// <summary>Anzeigetext einer Bedrohung für Kachel und Bericht.</summary>
    public static string Describe(ThreatRecord threat)
    {
        var when = threat.DetectedAt is DateTime d ? $" · {d:dd.MM.yyyy HH:mm}" : string.Empty;
        return $"{SeverityText(threat.SeverityId)} · {StatusText(threat.StatusId)}{when}";
    }
}
