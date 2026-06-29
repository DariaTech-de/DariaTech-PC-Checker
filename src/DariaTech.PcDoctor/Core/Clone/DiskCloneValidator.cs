using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Core.Clone;

/// <summary>Ergebnis der Klon-Sicherheitsprüfung.</summary>
public sealed record CloneValidation(
    bool CanClone,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Bei Erfolg zusätzlich getippter Bestätigungstext, den der Nutzer eingeben muss.</summary>
    public const string ConfirmWord = "KLONEN";
}

/// <summary>
/// Prüft eine geplante Klon-Aktion auf Gefahren (Datenverlust). Hartes Blockieren
/// bei System-/Startplatte als Ziel, identischer Quelle/Ziel oder zu kleinem Ziel.
/// Rein funktional – daher vollständig testbar.
/// </summary>
public static class DiskCloneValidator
{
    public static CloneValidation Validate(PhysicalDisk? source, PhysicalDisk? target)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (source is null) errors.Add("Bitte eine Quell-Platte wählen.");
        if (target is null) errors.Add("Bitte eine Ziel-Platte wählen.");

        if (source is not null && target is not null)
        {
            if (source.Number == target.Number)
                errors.Add("Quelle und Ziel sind dasselbe Laufwerk.");

            if (target.IsProtected)
                errors.Add($"Ziel „{target.Name}“ ist die System-/Startplatte – Klonen darauf ist gesperrt.");

            if (target.SizeBytes < source.SizeBytes)
                errors.Add($"Ziel ({target.SizeText}) ist kleiner als die Quelle ({source.SizeText}).");

            // Hinweise (kein Blocker)
            warnings.Add($"ALLE Daten auf dem Ziel „{target.Name}“ ({target.SizeText}, {target.Bus}) werden unwiderruflich überschrieben.");

            if (!string.Equals(source.Health, "Healthy", StringComparison.OrdinalIgnoreCase))
                warnings.Add($"Quelle meldet SMART-Status „{source.Health}“ – defekte Sektoren möglich; ddrescue-Imaging (überspringt/wiederholt) ist hier richtig.");

            if (target.SizeBytes > source.SizeBytes)
                warnings.Add("Ziel ist größer als die Quelle – der überschüssige Platz bleibt zunächst ungenutzt (Partition später erweitern).");
        }

        return new CloneValidation(errors.Count == 0, errors, warnings);
    }
}
