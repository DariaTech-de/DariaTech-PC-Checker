namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Führt alle registrierten Prüfungen aus, sammelt die Ergebnisse und
/// ermittelt die Gesamt-Ampel. Ein Fehler in einer Prüfung bricht den
/// Gesamtscan nie ab.
/// </summary>
public sealed class DiagnosticEngine
{
    private readonly IEnumerable<ICheck> _checks;

    public DiagnosticEngine(IEnumerable<ICheck> checks) => _checks = checks;

    public async Task<IReadOnlyList<CheckResult>> RunAllAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var all = new List<CheckResult>();

        foreach (var check in _checks)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Prüfe: {check.Area} …");

            try
            {
                all.AddRange(await check.RunAsync(ct).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                all.Add(new CheckResult(check.Area, "Hinweis",
                    $"nicht prüfbar ({ex.Message})", Severity.Info));
            }
        }

        return all;
    }

    /// <summary>Schlechtester Status über alle Ergebnisse = Gesamt-Ampel.</summary>
    public static Severity Overall(IEnumerable<CheckResult> results)
    {
        if (results.Any(r => r.Severity == Severity.Critical)) return Severity.Critical;
        if (results.Any(r => r.Severity == Severity.Warning)) return Severity.Warning;
        return Severity.Ok;
    }
}
