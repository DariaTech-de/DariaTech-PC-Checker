using System.Globalization;
using System.Management;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Update-Korrelation: listet die zuletzt installierten Windows-Updates und prüft,
/// ob die protokollierten Programmabstürze zeitlich kurz nach einem Update
/// begonnen haben. Beantwortet die häufigste Kundenfrage nach einem Update-Problem
/// („seit dem Update geht X nicht mehr") mit Daten statt Vermutung. Rein lesend.
/// </summary>
public sealed class UpdateStabilityCheck : ICheck
{
    private const int Days = 30;

    public string Area => "Updates & Stabilität";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            var updates = ReadRecentUpdates(Days, ct);
            if (updates.Count == 0)
            {
                results.Add(new CheckResult(Area, "Zuletzt installiert",
                    $"keine Updates in den letzten {Days} Tagen", Severity.Info));
            }
            else
            {
                results.Add(new CheckResult(Area, "Zuletzt installiert",
                    $"{updates.Count} Update(s) in {Days} Tagen", Severity.Info));

                foreach (var u in updates.Take(5))
                    results.Add(new CheckResult(Area, $" – {u.HotFixId}",
                        u.InstalledOn.ToString("dd.MM.yyyy"), Severity.Info));
            }

            // Zeitlichen Zusammenhang zu Abstürzen herstellen.
            try
            {
                var groups = AppCrashAnalyzer.Group(AppCrashLogReader.Read(Days, ct));
                var onset = UpdateCorrelator.CrashOnset(groups);

                if (onset is null)
                {
                    results.Add(new CheckResult(Area, "Zusammenhang",
                        "keine auffälligen Abstürze – kein Update-Verdacht", Severity.Ok));
                    return results;
                }

                var suspect = UpdateCorrelator.FindSuspect(updates, onset);
                if (suspect is null)
                {
                    results.Add(new CheckResult(Area, "Zusammenhang",
                        $"Abstürze seit {onset:dd.MM.yyyy}, kein Update kurz davor", Severity.Info,
                        Detail: "Die Abstürze begannen nicht unmittelbar nach einem Update – die Ursache " +
                                "liegt daher eher bei Treibern, Drittsoftware oder beschädigten Systemdateien."));
                    return results;
                }

                results.Add(new CheckResult(Area, "Update-Verdacht",
                    $"{suspect.HotFixId} vom {suspect.InstalledOn:dd.MM.yyyy}", Severity.Warning,
                    Detail: $"Die Abstürze begannen am {onset:dd.MM.yyyy} – kurz nach der Installation von " +
                            $"{suspect.HotFixId}. Das ist ein starker Hinweis auf ein fehlerhaftes Update.",
                    Tip: "Empfohlene Reihenfolge: 1) „Systemdateien reparieren (SFC + DISM)“ ausführen – " +
                         "das behebt die meisten Update-Schäden. 2) Nach neueren Updates suchen; Microsoft " +
                         $"korrigiert solche Fehler meist im Folgeupdate. 3) Erst wenn das nicht hilft, " +
                         $"{suspect.HotFixId} deinstallieren (Einstellungen → Windows Update → Updateverlauf → " +
                         "Updates deinstallieren).",
                    OpenTarget: "ms-settings:windowsupdate-history"));
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                results.Add(new CheckResult(Area, "Zusammenhang", "nicht prüfbar", Severity.Info));
            }

            return results;
        }, ct);

    /// <summary>Installierte Updates der letzten Tage (Win32_QuickFixEngineering), neueste zuerst.</summary>
    private static List<InstalledUpdate> ReadRecentUpdates(int days, CancellationToken ct)
    {
        var list = new List<InstalledUpdate>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT HotFixID, InstalledOn FROM Win32_QuickFixEngineering");

            var cutoff = DateTime.Now.AddDays(-days);
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                ct.ThrowIfCancellationRequested();
                var id = obj["HotFixID"]?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                var installed = ParseInstalledOn(obj["InstalledOn"]?.ToString());
                if (installed is DateTime d && d >= cutoff)
                    list.Add(new InstalledUpdate(id, d));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* WMI nicht verfügbar */ }

        return list.OrderByDescending(u => u.InstalledOn).ToList();
    }

    /// <summary>
    /// Robustes Parsen von <c>InstalledOn</c>: je nach System steht dort ein
    /// lokal formatiertes Datum oder ein US-Format – beides berücksichtigen.
    /// </summary>
    public static DateTime? ParseInstalledOn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var local))
            return local;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var inv))
            return inv;

        string[] formats = { "M/d/yyyy", "MM/dd/yyyy", "d.M.yyyy", "dd.MM.yyyy", "yyyyMMdd" };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
            return exact;

        return null;
    }
}
