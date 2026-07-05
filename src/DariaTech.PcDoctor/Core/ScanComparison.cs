namespace DariaTech.PcDoctor.Core;

/// <summary>Severity-Änderung eines Bereichs zwischen zwei Scans.</summary>
public sealed record AreaChange(string Area, Severity Before, Severity After);

/// <summary>
/// Ergebnis eines Vorher/Nachher-Vergleichs zweier Scans (Ausgangszustand vs.
/// aktueller Zustand) – für die sichtbare Erfolgskontrolle nach den Reparaturen
/// im Kundenbericht. UI-frei.
/// </summary>
public sealed class ScanComparison
{
    public DateTime BeforeTime { get; init; }
    public DateTime AfterTime { get; init; }

    public int ScoreBefore { get; init; }
    public int ScoreAfter { get; init; }
    public int CriticalBefore { get; init; }
    public int CriticalAfter { get; init; }
    public int WarningBefore { get; init; }
    public int WarningAfter { get; init; }

    /// <summary>Bereiche, deren Ampel sich verbessert hat.</summary>
    public IReadOnlyList<AreaChange> Improved { get; init; } = Array.Empty<AreaChange>();

    /// <summary>Bereiche, deren Ampel sich verschlechtert hat.</summary>
    public IReadOnlyList<AreaChange> Worsened { get; init; } = Array.Empty<AreaChange>();

    public int ScoreDelta => ScoreAfter - ScoreBefore;

    /// <summary>True, wenn es überhaupt etwas zu zeigen gibt (sonst Abschnitt weglassen).</summary>
    public bool HasChanges =>
        Improved.Count > 0 || Worsened.Count > 0 ||
        ScoreDelta != 0 || CriticalBefore != CriticalAfter || WarningBefore != WarningAfter;

    public string Trend => ScoreDelta > 0 ? "Verbesserung"
        : ScoreDelta < 0 ? "Verschlechterung"
        : "unverändert";

    /// <summary>Kompakte Kopfzeile für Dashboard-Banner und Bericht.</summary>
    public string Headline =>
        $"Gesundheit {ScoreBefore} → {ScoreAfter} ({(ScoreDelta >= 0 ? "+" : "")}{ScoreDelta}) · " +
        $"kritisch {CriticalBefore} → {CriticalAfter} · Warnungen {WarningBefore} → {WarningAfter}";
}

/// <summary>
/// Vergleicht zwei Befund-Sätze bereichsweise (rein funktional, gut testbar).
/// Die Bereichs-Ampel ergibt sich wie im Dashboard aus dem schlechtesten
/// Einzelergebnis (<see cref="DiagnosticEngine.Overall"/>).
/// </summary>
public static class ScanComparer
{
    public static ScanComparison Compare(
        IReadOnlyList<CheckResult> before, DateTime beforeTime,
        IReadOnlyList<CheckResult> after, DateTime afterTime)
    {
        var beforeAreas = ByArea(before);
        var afterAreas = ByArea(after);

        var improved = new List<AreaChange>();
        var worsened = new List<AreaChange>();

        foreach (var (area, afterSev) in afterAreas)
        {
            if (!beforeAreas.TryGetValue(area, out var beforeSev)) continue; // neu -> ignorieren
            var delta = Rank(afterSev) - Rank(beforeSev);
            if (delta < 0) improved.Add(new AreaChange(area, beforeSev, afterSev));
            else if (delta > 0) worsened.Add(new AreaChange(area, beforeSev, afterSev));
        }

        return new ScanComparison
        {
            BeforeTime = beforeTime,
            AfterTime = afterTime,
            ScoreBefore = ReportExporter.HealthScore(before),
            ScoreAfter = ReportExporter.HealthScore(after),
            CriticalBefore = Count(before, Severity.Critical),
            CriticalAfter = Count(after, Severity.Critical),
            WarningBefore = Count(before, Severity.Warning),
            WarningAfter = Count(after, Severity.Warning),
            Improved = improved,
            Worsened = worsened
        };
    }

    private static Dictionary<string, Severity> ByArea(IReadOnlyList<CheckResult> results)
        => results.GroupBy(r => r.Area)
                  .ToDictionary(g => g.Key, g => DiagnosticEngine.Overall(g));

    private static int Rank(Severity s) => s switch
    {
        Severity.Critical => 2,
        Severity.Warning => 1,
        _ => 0
    };

    private static int Count(IReadOnlyList<CheckResult> results, Severity s)
        => results.Count(r => r.Severity == s);
}
