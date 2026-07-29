using System.Diagnostics.Eventing.Reader;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>
/// Liest Anwendungsabstürze aus dem Ereignisprotokoll „Application":
/// Event-ID 1000 (Application Error, mit verursachendem Modul) und
/// 1002 (Application Hang – Anwendung reagiert nicht mehr).
/// Fehler werden abgefangen; im Zweifel kommt eine leere Liste zurück.
/// </summary>
public static class AppCrashLogReader
{
    private const int MaxRecords = 400;

    /// <summary>Absturzereignisse der letzten <paramref name="days"/> Tage.</summary>
    public static IReadOnlyList<CrashEvent> Read(int days = 30, CancellationToken ct = default)
    {
        var crashes = new List<CrashEvent>();
        try
        {
            var since = DateTime.UtcNow.AddDays(-days).ToString("o");
            var xpath =
                "*[System[(EventID=1000 or EventID=1002) and " +
                $"TimeCreated[@SystemTime>='{since}']]]";

            var query = new EventLogQuery("Application", PathType.LogName, xpath)
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query);
            for (EventRecord? rec = reader.ReadEvent();
                 rec is not null && crashes.Count < MaxRecords;
                 rec = reader.ReadEvent())
            {
                ct.ThrowIfCancellationRequested();
                using (rec)
                {
                    var when = rec.TimeCreated ?? DateTime.Now;
                    var props = SafeProperties(rec);

                    // Bei 1000 (Application Error): [0] = App, [3] = verursachendes Modul.
                    // Bei 1002 (Application Hang): [0] = App, kein Modul.
                    var app = props.Count > 0 ? props[0] : string.Empty;
                    var module = rec.Id == 1000 && props.Count > 3 ? props[3] : string.Empty;

                    // Ein Modul gleich dem App-Namen sagt nichts aus -> weglassen.
                    if (string.Equals(module, app, StringComparison.OrdinalIgnoreCase))
                        module = string.Empty;

                    if (!string.IsNullOrWhiteSpace(app))
                        crashes.Add(new CrashEvent(app, module, when));
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Protokoll nicht lesbar – leere Liste */ }

        return crashes;
    }

    private static List<string> SafeProperties(EventRecord rec)
    {
        try
        {
            return rec.Properties
                .Select(p => p.Value?.ToString() ?? string.Empty)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}
