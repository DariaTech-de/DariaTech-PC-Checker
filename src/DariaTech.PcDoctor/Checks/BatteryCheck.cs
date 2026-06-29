using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Akku (nur Notebooks): ermittelt den Verschleiß aus
/// <c>BatteryStaticData.DesignedCapacity</c> und
/// <c>BatteryFullChargedCapacity.FullChargedCapacity</c> (root\wmi).
/// Verschleiß &gt; 40 % → Warnung. Ohne Akku liefert der Check keine Zeilen.
/// </summary>
public sealed class BatteryCheck : ICheck
{
    public string Area => "Akku";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            // Liegt überhaupt ein Akku vor?
            int? charge = null;
            using (var batSearcher = new ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining FROM Win32_Battery"))
            {
                foreach (ManagementBaseObject bat in batSearcher.Get())
                {
                    charge = Convert.ToInt32(bat["EstimatedChargeRemaining"] ?? 0);
                    break;
                }
            }

            if (charge is null)
                return results; // Desktop ohne Akku → Bereich entfällt

            try
            {
                var scope = new ManagementScope(@"\\.\root\wmi");
                scope.Connect();

                var designed = ReadFirstLong(scope,
                    "SELECT DesignedCapacity FROM BatteryStaticData", "DesignedCapacity");
                var full = ReadFirstLong(scope,
                    "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity", "FullChargedCapacity");

                if (designed.HasValue && designed.Value > 0 && full.HasValue)
                {
                    var wear = (int)Math.Round((1 - (double)full.Value / designed.Value) * 100);
                    var text = $"{wear} % Verschleiß ({full} / {designed} mWh)";
                    results.Add(wear > 40
                        ? new CheckResult(Area, "Akku-Zustand", text, Severity.Warning,
                            $"Akku zu {wear} % verschlissen – Tausch könnte sinnvoll sein.",
                            Tip: "Ein Akkutausch bringt die Laufzeit zurück. Für Details unten „Akkubericht “ +
                                 "erstellen" nutzen (zeigt Verlauf und Kapazität). Schonung: den Akku möglichst " +
                                 "nicht dauerhaft bei 100 % am Netz lassen und Tiefentladung vermeiden.")
                        : new CheckResult(Area, "Akku-Zustand", text, Severity.Ok));
                    return results;
                }
            }
            catch { /* Fällt auf Ladestand zurück */ }

            results.Add(new CheckResult(Area, "Ladestand", $"{charge} %", Severity.Info));
            return results;
        }, ct);

    private static long? ReadFirstLong(ManagementScope scope, string query, string property)
    {
        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
        foreach (ManagementBaseObject obj in searcher.Get())
        {
            if (obj[property] is null) return null;
            return Convert.ToInt64(obj[property]);
        }
        return null;
    }
}
