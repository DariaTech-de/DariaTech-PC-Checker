using System.Diagnostics.Eventing.Reader;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Ereignisprotokoll: zählt kritische Fehler (Level 1–2) im Log „System" der
/// letzten 7 Tage, listet die häufigsten Quellen und weist auf unerwartete
/// Abschaltungen hin (Kernel-Power, Event-ID 41).
/// </summary>
public sealed class EventLogCheck : ICheck
{
    public string Area => "Ereignisprotokoll (letzte 7 Tage)";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            try
            {
                var since = DateTime.UtcNow.AddDays(-7).ToString("o");
                var xpath =
                    $"*[System[(Level=1 or Level=2) and TimeCreated[@SystemTime>='{since}']]]";

                var query = new EventLogQuery("System", PathType.LogName, xpath)
                {
                    ReverseDirection = true
                };

                var providerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int total = 0;
                int kernelPower41 = 0;

                using (var reader = new EventLogReader(query))
                {
                    for (EventRecord? rec = reader.ReadEvent(); rec is not null && total < 200;
                         rec = reader.ReadEvent())
                    {
                        ct.ThrowIfCancellationRequested();
                        using (rec)
                        {
                            total++;
                            var provider = rec.ProviderName ?? "Unbekannt";
                            providerCounts[provider] =
                                providerCounts.GetValueOrDefault(provider) + 1;
                            if (rec.Id == 41) kernelPower41++;
                        }
                    }
                }

                if (total == 0)
                {
                    results.Add(new CheckResult(Area, "Status",
                        "keine kritischen Fehler", Severity.Ok));
                    return results;
                }

                results.Add(new CheckResult(Area, "Kritische Fehler",
                    $"{total} Einträge",
                    total > 20 ? Severity.Warning : Severity.Info,
                    total > 20 ? $"Auffällig viele Systemfehler ({total} in 7 Tagen)." : null));

                foreach (var kv in providerCounts.OrderByDescending(k => k.Value).Take(5))
                    results.Add(new CheckResult(Area, $" – {kv.Key}", $"{kv.Value}x", Severity.Info));

                if (kernelPower41 > 0)
                    results.Add(new CheckResult(Area, "Unerwartete Abschaltung",
                        $"{kernelPower41}x (Kernel-Power 41)", Severity.Warning,
                        $"{kernelPower41}x unerwartete Abschaltung (Kernel-Power 41) – Netzteil/Treiber prüfen."));
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                results.Add(new CheckResult(Area, "Hinweis",
                    "Protokoll nicht lesbar", Severity.Info));
            }

            return results;
        }, ct);
}
