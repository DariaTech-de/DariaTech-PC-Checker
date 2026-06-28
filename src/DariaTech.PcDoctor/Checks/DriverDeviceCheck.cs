using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Treiber &amp; Geräte (<c>Win32_PnPEntity</c>): listet Geräte mit
/// <c>ConfigManagerErrorCode != 0</c> als Problemgeräte (Warnung).
/// </summary>
public sealed class DriverDeviceCheck : ICheck
{
    public string Area => "Treiber & Geräte";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            var problems = new List<string>();

            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
            {
                foreach (ManagementBaseObject obj in searcher.Get())
                {
                    ct.ThrowIfCancellationRequested();
                    var name = obj["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        problems.Add(name!);
                }
            }

            if (problems.Count == 0)
            {
                results.Add(new CheckResult(Area, "Status",
                    "alle Geräte in Ordnung", Severity.Ok));
            }
            else
            {
                foreach (var name in problems)
                    results.Add(new CheckResult(Area, "Problem", name, Severity.Warning));
                results.Add(new CheckResult(Area, "Zusammenfassung",
                    $"{problems.Count} Gerät(e) mit Treiberproblem", Severity.Warning,
                    $"{problems.Count} Gerät(e) mit Treiberproblem im Geräte-Manager."));
            }

            return results;
        }, ct);
}
