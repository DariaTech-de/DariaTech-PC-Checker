namespace DariaTech.PcDoctor.Core;

/// <summary>Ergebnis einer ausgeführten Reparatur.</summary>
public sealed record FixOutcome(bool Success, string Message);

/// <summary>
/// Eine Reparatur-Aktion. UI-frei. Systemverändernde Aktionen werden vom
/// RepairService NUR nach Nutzerbestätigung und (falls RequiresRestorePoint)
/// nach Anlegen eines Wiederherstellungspunkts ausgeführt.
/// </summary>
public interface IFixAction
{
    string Title { get; }
    string Description { get; }

    /// <summary>Vor Ausführung einen Systemwiederherstellungspunkt anlegen?</summary>
    bool RequiresRestorePoint { get; }

    /// <summary>Ist die Aktion umkehrbar (z. B. deaktivieren statt löschen)?</summary>
    bool IsReversible { get; }

    /// <param name="progress">Live-Fortschritt/Log für die UI.</param>
    Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default);
}
