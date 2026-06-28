using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Prüft den freien Speicherplatz aller lokalen Festplatten.
/// Ampel-Regel (wie im PowerShell-Prototyp): &lt; 15 % frei = Warnung,
/// &lt; 10 % frei = Kritisch.
///
/// Mustervorlage für alle weiteren Checks: WMI lesen, Werte mappen,
/// Severity nach Schwelle setzen, Fehler intern abfangen.
/// </summary>
public sealed class DiskSpaceCheck : ICheck
{
    public string Area => "Datenträger – Speicherplatz";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
    {
        var results = new List<CheckResult>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT DeviceID, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3");

        foreach (ManagementBaseObject obj in searcher.Get())
        {
            ct.ThrowIfCancellationRequested();

            var id = (string)obj["DeviceID"];
            var size = Convert.ToInt64(obj["Size"] ?? 0L);
            var free = Convert.ToInt64(obj["FreeSpace"] ?? 0L);
            if (size == 0) continue;

            var freePct = (int)Math.Round(free * 100.0 / size);
            var value = $"{ToGb(free)} frei von {ToGb(size)} ({freePct} %)";

            var severity = freePct switch
            {
                < 10 => Severity.Critical,
                < 15 => Severity.Warning,
                _ => Severity.Ok
            };

            string? detail = severity switch
            {
                Severity.Critical => $"Laufwerk {id} ist fast voll – aufräumen empfohlen.",
                Severity.Warning => $"Laufwerk {id} wird knapp.",
                _ => null
            };

            results.Add(new CheckResult(Area, $"Laufwerk {id}", value, severity, detail));
        }

        return Task.FromResult<IReadOnlyList<CheckResult>>(results);
    }

    private static string ToGb(long bytes) => $"{bytes / 1024d / 1024d / 1024d:N1} GB";
}
