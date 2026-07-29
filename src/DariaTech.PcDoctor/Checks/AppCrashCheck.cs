using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Absturz-Analyse: fasst Anwendungsabstürze der letzten 30 Tage aus dem
/// Ereignisprotokoll zusammen (welche App, welches verursachende Modul, wie oft,
/// seit wann) und nennt je Fall die passenden Reparaturschritte. Beantwortet
/// typische Kundenmeldungen wie „Der Explorer schließt sich beim Suchen" direkt.
/// Rein lesend.
/// </summary>
public sealed class AppCrashCheck : ICheck
{
    private const int Days = 30;

    public string Area => "Programmabstürze (letzte 30 Tage)";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            IReadOnlyList<CrashGroup> groups;
            try
            {
                groups = AppCrashAnalyzer.Group(AppCrashLogReader.Read(Days, ct));
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                results.Add(new CheckResult(Area, "Hinweis", "Protokoll nicht lesbar", Severity.Info));
                return results;
            }

            if (groups.Count == 0)
            {
                results.Add(new CheckResult(Area, "Status",
                    "keine Anwendungsabstürze protokolliert", Severity.Ok));
                return results;
            }

            var total = groups.Sum(g => g.Count);
            var worst = groups[0];
            var overallSeverity = AppCrashAnalyzer.SeverityFor(worst);

            results.Add(new CheckResult(Area, "Gesamt",
                $"{total} Abstürze in {groups.Count} Anwendung(en)",
                overallSeverity,
                Detail: overallSeverity >= Severity.Warning
                    ? $"Häufigster Fall: „{worst.App}“ – {AppCrashAnalyzer.Describe(worst)}."
                    : null,
                Tip: overallSeverity >= Severity.Warning ? AppCrashAnalyzer.TipFor(worst) : null,
                OpenTarget: overallSeverity >= Severity.Warning ? "eventvwr.msc" : null));

            // Die auffälligsten Fälle einzeln – mit Modul, damit die Ursache benennbar ist.
            foreach (var g in groups.Take(6))
            {
                var severity = AppCrashAnalyzer.SeverityFor(g);
                var label = string.IsNullOrEmpty(g.Module)
                    ? $" – {g.App}"
                    : $" – {g.App} (in {g.Module})";

                results.Add(new CheckResult(Area, label,
                    AppCrashAnalyzer.Describe(g),
                    severity,
                    Detail: string.IsNullOrEmpty(g.Module)
                        ? null
                        : $"Verursachendes Modul: {g.Module}",
                    Tip: severity >= Severity.Warning ? AppCrashAnalyzer.TipFor(g) : null));
            }

            return results;
        }, ct);
}
