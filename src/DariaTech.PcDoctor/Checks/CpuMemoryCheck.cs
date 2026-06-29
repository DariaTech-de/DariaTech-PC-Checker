using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Prozessor &amp; Arbeitsspeicher: CPU-Name/Kerne, CPU-Auslastung (&gt; 90 % →
/// Warnung), RAM-Belegung (&gt; 90 % → Warnung) und Anzahl verbauter Riegel.
/// </summary>
public sealed class CpuMemoryCheck : ICheck
{
    public string Area => "Prozessor & Arbeitsspeicher";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            using (var cpuSearcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, LoadPercentage FROM Win32_Processor"))
            {
                foreach (ManagementBaseObject cpu in cpuSearcher.Get())
                {
                    results.Add(new CheckResult(Area, "CPU",
                        $"{cpu["Name"]}".Trim(), Severity.Info));
                    results.Add(new CheckResult(Area, "Kerne",
                        $"{cpu["NumberOfCores"]} Kerne / {cpu["NumberOfLogicalProcessors"]} Threads",
                        Severity.Info));

                    if (cpu["LoadPercentage"] is not null)
                    {
                        var load = Convert.ToInt32(cpu["LoadPercentage"]);
                        results.Add(new CheckResult(Area, "CPU-Auslastung", $"{load} %",
                            load > 90 ? Severity.Warning : Severity.Ok,
                            Detail: load > 90 ? "CPU dauerhaft stark ausgelastet." : null,
                            Tip: load > 90
                                ? "So finden Sie die Ursache: Task-Manager öffnen (Strg+Umschalt+Esc) → " +
                                  "Reiter „Prozesse“ → nach Spalte „CPU“ sortieren. Hängt ein Programm dauerhaft " +
                                  "hoch, beenden oder neu starten; danach prüfen, ob es im Autostart nötig ist."
                                : null,
                            OpenTarget: load > 90 ? "taskmgr" : null));
                    }
                    break; // erster Prozessor genügt
                }
            }

            using (var osSearcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
            {
                foreach (ManagementBaseObject os in osSearcher.Get())
                {
                    var totalKb = Convert.ToInt64(os["TotalVisibleMemorySize"] ?? 0L);
                    var freeKb = Convert.ToInt64(os["FreePhysicalMemory"] ?? 0L);
                    if (totalKb > 0)
                    {
                        var usedPct = (int)Math.Round((totalKb - freeKb) * 100.0 / totalKb);
                        var totalGb = totalKb / 1024d / 1024d;
                        var text = $"{totalGb:N1} GB gesamt, {usedPct} % belegt";
                        results.Add(usedPct > 90
                            ? new CheckResult(Area, "Arbeitsspeicher", text, Severity.Warning,
                                $"Arbeitsspeicher zu {usedPct} % belegt – möglicher Engpass.",
                                Tip: "So entlasten: Task-Manager öffnen (Strg+Umschalt+Esc) → „Prozesse“ → nach " +
                                     "Spalte „Arbeitsspeicher“ sortieren und nicht benötigte, speicherhungrige " +
                                     "Programme (oft viele Browser-Tabs) schließen. Hilft das dauerhaft nicht, ist " +
                                     "der PC für die Nutzung evtl. mit zu wenig RAM ausgestattet.",
                                OpenTarget: "taskmgr")
                            : new CheckResult(Area, "Arbeitsspeicher", text, Severity.Ok));
                    }
                    break;
                }
            }

            using (var memSearcher = new ManagementObjectSearcher(
                "SELECT Capacity FROM Win32_PhysicalMemory"))
            {
                var count = memSearcher.Get().Count;
                results.Add(new CheckResult(Area, "RAM-Module",
                    $"{count} Riegel verbaut", Severity.Info));
            }

            return results;
        }, ct);
}
