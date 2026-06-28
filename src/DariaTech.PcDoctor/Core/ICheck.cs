namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Eine Diagnose-Prüfung. Implementierungen müssen UI-frei sein und Fehler
/// intern abfangen (statt zu werfen ein CheckResult mit Severity.Info/Warning
/// und Hinweis "nicht prüfbar" zurückgeben).
/// </summary>
public interface ICheck
{
    /// <summary>Bereich/Kachel, unter der die Ergebnisse erscheinen.</summary>
    string Area { get; }

    Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default);
}
