using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Autostart-Programme (<c>Win32_StartupCommand</c>): Anzahl + erste Einträge.
/// Mehr als 15 Einträge → Hinweis (Warnung), da viele den Start bremsen.
/// </summary>
public sealed class StartupCheck : ICheck
{
    public string Area => "Autostart-Programme";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            var entries = new List<(string Name, string Location)>();

            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name, Location FROM Win32_StartupCommand"))
            {
                foreach (ManagementBaseObject obj in searcher.Get())
                {
                    ct.ThrowIfCancellationRequested();
                    entries.Add(($"{obj["Name"]}", $"{obj["Location"]}"));
                }
            }

            var count = entries.Count;
            results.Add(new CheckResult(Area, "Anzahl",
                $"{count} Einträge",
                count > 15 ? Severity.Warning : Severity.Info,
                count > 15 ? $"{count} Autostart-Einträge – viele davon bremsen den Start." : null));

            foreach (var (name, location) in entries.Take(8))
                results.Add(new CheckResult(Area, $" – {name}", location, Severity.Info));

            return results;
        }, ct);
}
