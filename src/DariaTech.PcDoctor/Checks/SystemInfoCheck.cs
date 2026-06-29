using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// System &amp; Betriebssystem: Gerät, Seriennummer, Windows-Build,
/// Installationsdatum, BIOS, Uptime. Uptime &gt; 14 Tage → Warnung.
/// </summary>
public sealed class SystemInfoCheck : ICheck
{
    public string Area => "System & Betriebssystem";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            var cs = QueryFirst("Win32_ComputerSystem");
            var os = QueryFirst("Win32_OperatingSystem");
            var bios = QueryFirst("Win32_BIOS");

            if (cs is not null)
                results.Add(Row("Gerät",
                    $"{cs["Manufacturer"]} {cs["Model"]}".Trim()));

            if (bios is not null)
            {
                results.Add(Row("Seriennummer", $"{bios["SerialNumber"]}"));
                results.Add(Row("BIOS/UEFI",
                    $"{bios["Manufacturer"]}  {bios["SMBIOSBIOSVersion"]}"));
            }

            results.Add(Row("Computername", Environment.MachineName));

            if (os is not null)
            {
                results.Add(Row("Windows",
                    $"{os["Caption"]} (Build {os["BuildNumber"]})"));

                var installed = ToDate(os["InstallDate"]);
                if (installed is not null)
                    results.Add(Row("Installiert am", installed.Value.ToString("dd.MM.yyyy")));

                var lastBoot = ToDate(os["LastBootUpTime"]);
                if (lastBoot is not null)
                {
                    var uptime = DateTime.Now - lastBoot.Value;
                    var text = $"{uptime.Days} Tage, {uptime.Hours} Std";
                    if (uptime.TotalDays > 14)
                        results.Add(new CheckResult(Area, "Letzter Neustart",
                            $"{text} (lange her)", Severity.Warning,
                            $"Seit {uptime.Days} Tagen kein Neustart – Neustart empfohlen."));
                    else
                        results.Add(new CheckResult(Area, "Letzter Neustart", text, Severity.Ok));
                }
            }

            return results;
        }, ct);

    private CheckResult Row(string label, string value)
        => new(Area, label, value, Severity.Info);

    private static ManagementBaseObject? QueryFirst(string wmiClass)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT * FROM {wmiClass}");
        foreach (ManagementBaseObject obj in searcher.Get())
            return obj;
        return null;
    }

    private static DateTime? ToDate(object? value)
    {
        if (value is null) return null;
        try { return ManagementDateTimeConverter.ToDateTime(value.ToString()); }
        catch { return null; }
    }
}
