using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Persistiert Kundenbefunde (Verlauf) und liefert sie zurück – für
/// Vorher/Nachher-Vergleiche über mehrere Einsätze hinweg.
/// </summary>
public interface IHistoryStore
{
    /// <summary>Alle Einträge, neueste zuerst.</summary>
    IReadOnlyList<HistoryEntry> All();

    /// <summary>
    /// Speichert den aktuellen Befund (legt den HTML-Bericht im Verlaufsordner ab,
    /// ergänzt den Index) und gibt den erzeugten Eintrag zurück.
    /// </summary>
    HistoryEntry Save(IReadOnlyList<CheckResult> results, ReportContext? context);
}
