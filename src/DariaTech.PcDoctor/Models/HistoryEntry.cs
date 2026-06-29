namespace DariaTech.PcDoctor.Models;

/// <summary>
/// Ein gespeicherter Befund im Kundenverlauf (für Vorher/Nachher-Vergleich
/// über mehrere Einsätze). Wird als JSON persistiert.
/// </summary>
public sealed class HistoryEntry
{
    public DateTime Timestamp { get; init; }
    public string Customer { get; init; } = string.Empty;
    public string Order { get; init; } = string.Empty;
    public string Technician { get; init; } = string.Empty;
    public string Computer { get; init; } = string.Empty;
    public int HealthScore { get; init; }
    public int CriticalCount { get; init; }
    public int WarningCount { get; init; }

    /// <summary>Pfad zur gespeicherten HTML-Berichtsdatei.</summary>
    public string ReportPath { get; init; } = string.Empty;

    public string Display =>
        $"{Timestamp:dd.MM.yyyy HH:mm} · {(string.IsNullOrWhiteSpace(Customer) ? "(ohne Kunde)" : Customer)} · " +
        $"{Computer} · Score {HealthScore}/100 · {CriticalCount} kritisch / {WarningCount} Warnungen";
}
