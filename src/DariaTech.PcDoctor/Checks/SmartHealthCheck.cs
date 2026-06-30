using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Datenträger-Gesundheit (SMART): liest <c>MSFT_PhysicalDisk.HealthStatus</c>
/// (root\Microsoft\Windows\Storage) und zusätzlich die klassische
/// Ausfallvorhersage <c>MSStorageDriver_FailurePredictStatus</c> (root\wmi).
/// HealthStatus „Warning“ → Warnung, alles andere außer „Healthy“ → Kritisch;
/// PredictFailure = true → Kritisch (sofort Backup/Tausch).
/// </summary>
public sealed class SmartHealthCheck : ICheck
{
    public string Area => "Datenträger – Gesundheit (SMART)";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                var query = new ObjectQuery(
                    "SELECT FriendlyName, MediaType, Size, HealthStatus FROM MSFT_PhysicalDisk");
                using var searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementBaseObject disk in searcher.Get())
                {
                    var name = $"{disk["FriendlyName"]}";
                    var media = MediaTypeText(disk["MediaType"]);
                    var size = ToGb(Convert.ToInt64(disk["Size"] ?? 0L));
                    var health = HealthText(disk["HealthStatus"]);
                    var text = $"{name}  ({media}, {size})";

                    results.Add(health switch
                    {
                        "Healthy" => new CheckResult(Area, "Status: OK", text, Severity.Ok),
                        "Warning" => new CheckResult(Area, "Status: WARNUNG", text, Severity.Warning,
                            $"Datenträger „{name}“ meldet SMART-Warnung – Backup prüfen.",
                            Tip: "Jetzt wichtig: zuerst alle wichtigen Daten sichern (externe Platte oder Cloud). " +
                                 "Eine SMART-Warnung ist oft der erste Hinweis auf einen baldigen Ausfall. " +
                                 "Bei einer schwächelnden Platte gilt: erst klonen (Tab „Klonen“), dann tauschen."),
                        _ => new CheckResult(Area, $"Status: {health}", text, Severity.Critical,
                            $"Datenträger „{name}“: {health} – möglicher Defekt, sofort Backup!",
                            Tip: "Sofort handeln: alle wichtigen Daten umgehend sichern und den Datenträger " +
                                 "ersetzen. Ist es das Systemlaufwerk, im Tab „Klonen“ 1:1 auf eine neue SSD/HDD " +
                                 "klonen, bevor es ganz ausfällt.")
                    });
                }
            }
            catch
            {
                results.Add(new CheckResult(Area, "Hinweis",
                    "SMART-Status nicht abrufbar (Storage-Namespace nicht verfügbar)",
                    Severity.Info));
            }

            // Klassische Ausfallvorhersage (auch für ältere HDDs)
            try
            {
                var scope = new ManagementScope(@"\\.\root\wmi");
                var query = new ObjectQuery(
                    "SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus");
                using var searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementBaseObject p in searcher.Get())
                {
                    if (p["PredictFailure"] is bool fail && fail)
                        results.Add(new CheckResult(Area, "Ausfallvorhersage",
                            $"FAIL ({p["InstanceName"]})", Severity.Critical,
                            "SMART sagt baldigen Ausfall voraus – Datenträger umgehend ersetzen!",
                            Tip: "Nicht warten: zuerst die Daten sichern, dann den Datenträger tauschen. " +
                                 "Beim Systemlaufwerk vorher im Tab „Klonen“ 1:1 auf einen neuen Datenträger klonen."));
                }
            }
            catch { /* Namespace nicht auf jedem System vorhanden – kein Fehler */ }

            return results;
        }, ct);

    private static string ToGb(long bytes) => $"{bytes / 1024d / 1024d / 1024d:N1} GB";

    private static string HealthText(object? value)
    {
        // MSFT_PhysicalDisk.HealthStatus: 0=Healthy, 1=Warning, 2=Unhealthy
        if (value is null) return "Unbekannt";
        return Convert.ToInt32(value) switch
        {
            0 => "Healthy",
            1 => "Warning",
            2 => "Unhealthy",
            _ => "Unbekannt"
        };
    }

    private static string MediaTypeText(object? value)
    {
        // MSFT_PhysicalDisk.MediaType: 3=HDD, 4=SSD, 5=SCM
        if (value is null) return "Datenträger";
        return Convert.ToInt32(value) switch
        {
            3 => "HDD",
            4 => "SSD",
            5 => "SCM",
            _ => "Datenträger"
        };
    }
}
