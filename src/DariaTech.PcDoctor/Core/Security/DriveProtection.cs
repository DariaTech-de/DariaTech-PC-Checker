namespace DariaTech.PcDoctor.Core.Security;

/// <summary>BitLocker-Zustand eines Laufwerks.</summary>
/// <param name="DriveLetter">Laufwerksbuchstabe, z. B. „C:“.</param>
/// <param name="ProtectionStatus">0 = aus, 1 = an, 2 = unbekannt.</param>
/// <param name="ConversionStatus">0 = entschlüsselt, 1 = verschlüsselt, 2/3 = läuft, 4/5 = angehalten.</param>
/// <param name="HasRecoveryPassword">Existiert ein Wiederherstellungsschlüssel?</param>
public sealed record BitLockerVolume(
    string DriveLetter,
    int ProtectionStatus,
    int ConversionStatus,
    bool HasRecoveryPassword);

/// <summary>
/// Bewertet BitLocker aus Sicht der Reparatur-Sicherheit.
///
/// Der entscheidende Punkt für die Werkstatt: Ist ein Laufwerk verschlüsselt und
/// der Wiederherstellungsschlüssel NICHT greifbar, können systemnahe Eingriffe
/// (Firmware, Startumgebung, Klonen) dazu führen, dass Windows beim nächsten
/// Start den Schlüssel verlangt – ohne ihn sind die Daten unwiederbringlich weg.
/// Deshalb ist genau dieser Fall kritisch, nicht die Verschlüsselung selbst.
///
/// Rein funktional und damit vollständig testbar.
/// </summary>
public static class BitLockerRules
{
    public const int ProtectionOff = 0;
    public const int ProtectionOn = 1;

    public const int FullyDecrypted = 0;
    public const int FullyEncrypted = 1;
    public const int EncryptionInProgress = 2;
    public const int DecryptionInProgress = 3;
    public const int EncryptionPaused = 4;
    public const int DecryptionPaused = 5;

    /// <summary>True, wenn das Laufwerk (auch teilweise) verschlüsselt ist.</summary>
    public static bool IsEncrypted(BitLockerVolume volume)
        => volume.ProtectionStatus == ProtectionOn
        || volume.ConversionStatus is FullyEncrypted or EncryptionInProgress or EncryptionPaused;

    /// <summary>Klartext für den Schutzstatus.</summary>
    public static string StatusText(BitLockerVolume volume) => volume.ConversionStatus switch
    {
        FullyEncrypted when volume.ProtectionStatus == ProtectionOn => "verschlüsselt, Schutz aktiv",
        FullyEncrypted => "verschlüsselt, Schutz ausgesetzt",
        EncryptionInProgress => "wird gerade verschlüsselt",
        DecryptionInProgress => "wird gerade entschlüsselt",
        EncryptionPaused => "Verschlüsselung angehalten",
        DecryptionPaused => "Entschlüsselung angehalten",
        FullyDecrypted => "nicht verschlüsselt",
        _ => volume.ProtectionStatus == ProtectionOn ? "Schutz aktiv" : "unbekannt"
    };

    /// <summary>Ampel aus Sicht der Reparatur-Sicherheit.</summary>
    public static Severity SeverityFor(BitLockerVolume volume)
    {
        // Verschlüsselt, aber kein Wiederherstellungsschlüssel -> echtes Datenverlustrisiko.
        if (IsEncrypted(volume) && !volume.HasRecoveryPassword) return Severity.Critical;

        // Laufender Vorgang: jetzt keine Eingriffe vornehmen.
        if (volume.ConversionStatus is EncryptionInProgress or DecryptionInProgress) return Severity.Warning;

        return Severity.Info;
    }

    /// <summary>Handlungsanweisung für den Techniker.</summary>
    public static string? AdviceFor(BitLockerVolume volume)
    {
        if (IsEncrypted(volume) && !volume.HasRecoveryPassword)
            return "ACHTUNG: Das Laufwerk ist verschlüsselt, es ist aber KEIN Wiederherstellungsschlüssel " +
                   "hinterlegt. Vor systemnahen Reparaturen (Startumgebung, Firmware, Klonen) unbedingt " +
                   "einen Schlüssel anlegen und beim Kunden sichern – sonst können die Daten dauerhaft " +
                   "unzugänglich werden.";

        if (volume.ConversionStatus is EncryptionInProgress or DecryptionInProgress)
            return "Die Ver-/Entschlüsselung läuft gerade. Bis zum Abschluss keine Reparaturen ausführen " +
                   "und den PC nicht ausschalten.";

        if (IsEncrypted(volume))
            return "Vor systemnahen Reparaturen sicherstellen, dass der Kunde seinen " +
                   "Wiederherstellungsschlüssel griffbereit hat (Microsoft-Konto, Ausdruck oder Datei).";

        return null;
    }
}

/// <summary>Bewertung der Systemwiederherstellung.</summary>
/// <param name="Severity">Ampel.</param>
/// <param name="Summary">Kurztext für die Kachel.</param>
/// <param name="Detail">Erläuterung.</param>
public sealed record RestoreProtectionResult(Severity Severity, string Summary, string Detail);

/// <summary>
/// Bewertet, ob vor Reparaturen eine Rückfallebene existiert: Ist der Systemschutz
/// aktiv und gibt es hinreichend aktuelle Wiederherstellungspunkte?
/// Rein funktional und damit vollständig testbar.
/// </summary>
public static class RestoreProtectionRules
{
    /// <summary>Ab diesem Alter gilt der jüngste Punkt als veraltet.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    /// <summary>
    /// Bewertet den Zustand.
    /// </summary>
    /// <param name="restorePoints">Zeitpunkte vorhandener Wiederherstellungspunkte.</param>
    /// <param name="now">Aktueller Zeitpunkt.</param>
    /// <param name="protectionDisabled">
    /// True, wenn der Systemschutz nachweislich abgeschaltet ist; null, wenn unbekannt.
    /// </param>
    public static RestoreProtectionResult Evaluate(
        IReadOnlyList<DateTime> restorePoints,
        DateTime now,
        bool? protectionDisabled = null)
    {
        if (protectionDisabled == true)
            return new RestoreProtectionResult(Severity.Warning,
                "Systemschutz ist abgeschaltet",
                "Ohne Systemschutz kann Windows keine Wiederherstellungspunkte anlegen – es gibt vor " +
                "Reparaturen keine Rückfallebene. Bitte über die Reparatur " +
                "„Systemwiederherstellung einschalten“ aktivieren.");

        if (restorePoints.Count == 0)
            return new RestoreProtectionResult(Severity.Warning,
                "keine Wiederherstellungspunkte vorhanden",
                "Es existiert kein Punkt, auf den sich das System zurücksetzen ließe. Vor größeren " +
                "Reparaturen einen anlegen – die App tut das bei systemverändernden Aktionen zwar " +
                "automatisch, ein geprüfter Ausgangspunkt ist aber sicherer.");

        var newest = restorePoints.Max();
        var age = now - newest;

        if (age > StaleAfter)
            return new RestoreProtectionResult(Severity.Info,
                $"{restorePoints.Count} Punkt(e), neuester {(int)age.TotalDays} Tage alt",
                "Der jüngste Wiederherstellungspunkt ist älter als 30 Tage. Für einen Rücksprung auf " +
                "den heutigen Zustand vorher einen frischen Punkt anlegen.");

        return new RestoreProtectionResult(Severity.Ok,
            $"{restorePoints.Count} Punkt(e), neuester vom {newest:dd.MM.yyyy}",
            "Es existiert eine aktuelle Rückfallebene für Reparaturen.");
    }
}
