using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Datenträger-Detailwerte aus der Hardwaresensorik (LibreHardwareMonitor):
/// je Datenträger Temperatur, SSD-Restlebensdauer, geschriebene Datenmenge (TBW)
/// und Belegung. Restlebensdauer &lt; 20 % → Warnung, &lt; 10 % → Kritisch;
/// Temperatur &gt; 60 °C → Warnung.
/// </summary>
public sealed class StorageDetailCheck : ICheck
{
    private readonly ISensorService _sensors;

    public StorageDetailCheck(ISensorService sensors) => _sensors = sensors;

    public string Area => "Datenträger – Detail";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            List<SensorReading> storage;
            try
            {
                storage = _sensors.Read()
                    .Where(r => r.HardwareType.Equals("Storage", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch
            {
                results.Add(new CheckResult(Area, "Hinweis",
                    "Detailwerte nicht prüfbar (Sensorik nicht verfügbar)", Severity.Info));
                return results;
            }

            if (storage.Count == 0)
            {
                results.Add(new CheckResult(Area, "Hinweis",
                    "keine Detail-Sensoren verfügbar", Severity.Info));
                return results;
            }

            foreach (var drive in storage.GroupBy(r => r.HardwareName))
            {
                results.Add(new CheckResult(Area, "Datenträger", drive.Key, Severity.Info));

                var life = Value(drive, SensorKind.Level, "Remaining Life");
                if (life is double l)
                    results.Add(new CheckResult(Area, "Restlebensdauer", $"{l:0} %",
                        l < 10 ? Severity.Critical : l < 20 ? Severity.Warning : Severity.Ok,
                        l < 20 ? "SSD-Lebensdauer niedrig – Austausch/Backup einplanen." : null));

                var temp = Value(drive, SensorKind.Temperature, null);
                if (temp is double t)
                    results.Add(new CheckResult(Area, "Temperatur", $"{t:0} °C",
                        t > 60 ? Severity.Warning : Severity.Ok,
                        t > 60 ? "Datenträger läuft heiß – Belüftung prüfen." : null));

                var written = Value(drive, SensorKind.Data, "Data Written");
                if (written is double w)
                    results.Add(new CheckResult(Area, "Geschrieben gesamt",
                        w >= 1024 ? $"{w / 1024:0.0} TB" : $"{w:0} GB", Severity.Info));

                var used = Value(drive, SensorKind.Load, "Used Space");
                if (used is double u)
                    results.Add(new CheckResult(Area, "Belegung", $"{u:0} %", Severity.Info));
            }

            return results;
        }, ct);

    private static double? Value(IEnumerable<SensorReading> readings, SensorKind kind, string? nameContains)
    {
        foreach (var r in readings)
        {
            if (r.Kind != kind) continue;
            if (nameContains is not null &&
                !r.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)) continue;
            return r.Value;
        }
        return null;
    }
}
