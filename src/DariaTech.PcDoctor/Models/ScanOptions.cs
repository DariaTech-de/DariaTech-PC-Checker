namespace DariaTech.PcDoctor.Models;

/// <summary>
/// Laufzeit-Optionen für den Scan. Als Singleton registriert; die UI setzt die
/// Werte, einzelne Checks lesen sie (z. B. die langsame Update-Prüfung).
/// </summary>
public sealed class ScanOptions
{
    /// <summary>Überspringt die langsame Windows-Update-Suche (Schnellmodus).</summary>
    public bool SkipWindowsUpdate { get; set; }
}
