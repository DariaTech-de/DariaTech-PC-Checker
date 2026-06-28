namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Ergebnis einer einzelnen Diagnose-Prüfung. UI-frei.
/// </summary>
/// <param name="Area">Bereich/Kachel, z. B. "Datenträger – Speicherplatz".</param>
/// <param name="Label">Kurzbezeichnung der Zeile, z. B. "Laufwerk C:".</param>
/// <param name="Value">Anzuzeigender Wert, z. B. "42,1 GB frei (12 %)".</param>
/// <param name="Severity">Ampel-Status.</param>
/// <param name="Detail">Optionaler Erklärtext für die Detailansicht.</param>
/// <param name="Fixes">Optional zugeordnete Reparaturen für dieses Ergebnis.</param>
public sealed record CheckResult(
    string Area,
    string Label,
    string Value,
    Severity Severity,
    string? Detail = null,
    IReadOnlyList<IFixAction>? Fixes = null);
