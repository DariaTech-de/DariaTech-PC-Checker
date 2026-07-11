using System.Linq;
using System.Management;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Models;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>
/// <see cref="ISensorService"/> auf Basis von LibreHardwareMonitor. Öffnet beim
/// ersten Zugriff den Sensor-Stack (lädt einen Kernel-Treiber für den
/// Hardwarezugriff – Adminrechte nötig). Schlägt das fehl, bleibt der Dienst
/// „nicht verfügbar", ohne zu werfen.
/// </summary>
public sealed class LibreHardwareSensorService : ISensorService
{
    private readonly ILogger<LibreHardwareSensorService> _log;
    private readonly object _gate = new();
    private readonly UpdateVisitor _visitor = new();
    private Computer? _computer;
    private bool _initialized;

    public LibreHardwareSensorService(ILogger<LibreHardwareSensorService> log) => _log = log;

    public bool IsAvailable
    {
        get
        {
            lock (_gate)
            {
                EnsureInitialized();
                // Verfügbar, wenn LibreHardwareMonitor läuft ODER zumindest die
                // ACPI-Thermalzone eine CPU-Temperatur liefert (z. B. HP EliteBook).
                return _computer is not null || TryReadAcpiCpuTemp() is not null;
            }
        }
    }

    public IReadOnlyList<SensorReading> Read()
    {
        lock (_gate)
        {
            EnsureInitialized();
            var readings = new List<SensorReading>();

            if (_computer is not null)
            {
                try
                {
                    _computer.Accept(_visitor);
                    foreach (var hw in _computer.Hardware)
                        Collect(hw, readings);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Sensorabfrage fehlgeschlagen");
                }
            }

            // Fallback: liefert der Treiber keine CPU-Temperatur, ACPI-Thermalzone nutzen.
            var hasCpuTemp = readings.Any(r =>
                r.Kind == SensorKind.Temperature &&
                r.HardwareType.Equals("Cpu", StringComparison.OrdinalIgnoreCase));
            if (!hasCpuTemp && TryReadAcpiCpuTemp() is double acpi)
                readings.Add(new SensorReading("ACPI-Thermalzone", "Cpu", "CPU (ACPI)",
                    SensorKind.Temperature, acpi));

            return readings;
        }
    }

    /// <summary>
    /// Liest die CPU-/System-Temperatur über <c>MSAcpi_ThermalZoneTemperature</c>
    /// (root\wmi). Funktioniert auf vielen Geräten auch ohne LHM-Treiber.
    /// </summary>
    private double? TryReadAcpiCpuTemp()
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\wmi");
            var query = new ObjectQuery("SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            using var searcher = new ManagementObjectSearcher(scope, query);

            double? best = null;
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                if (obj["CurrentTemperature"] is null) continue;
                // Wert in Zehntel-Kelvin -> °C.
                var celsius = Convert.ToDouble(obj["CurrentTemperature"]) / 10.0 - 273.15;
                if (celsius is > 0 and < 130 && (best is null || celsius > best))
                    best = celsius;
            }
            return best;
        }
        catch
        {
            return null;
        }
    }

    private void Collect(IHardware hardware, List<SensorReading> into)
    {
        foreach (var sub in hardware.SubHardware)
            Collect(sub, into);

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not float value || float.IsNaN(value)) continue;
            var kind = Map(sensor.SensorType);
            if (kind is null) continue;

            // Grenzwert-„Sensoren" (z. B. NVMe „Critical/Warning Temperature") sind
            // fest hinterlegte Schwellen, keine Live-Messwerte -> nicht anzeigen.
            if (IsThresholdSensor(sensor.Name)) continue;

            // Physikalisch unplausible Temperatur (<= 0 °C bei laufendem Windows)
            // bedeutet fast immer „nicht lesbar" (blockierter/fehlender Kernel-
            // Treiber, z. B. CPU-Tctl). Solche Werte NICHT als echte 0 ausgeben –
            // sonst wirkt eine defekte Messung wie „kalt/alles gut".
            if (kind == SensorKind.Temperature && value <= 0f) continue;

            into.Add(new SensorReading(
                hardware.Name,
                hardware.HardwareType.ToString(),
                sensor.Name,
                kind.Value,
                value));
        }
    }

    /// <summary>
    /// True für Schwellenwert-Attribute, die manche Datenträger als „Sensor"
    /// melden (feste Grenzen statt Messwerte) – gehören nicht in die Anzeige.
    /// </summary>
    private static bool IsThresholdSensor(string name)
        => name.Contains("Critical Temperature", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Warning Temperature", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Throttle", StringComparison.OrdinalIgnoreCase);

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsControllerEnabled = true
            };
            computer.Open();
            _computer = computer;
            _log.LogInformation("Sensor-Stack geöffnet ({Count} Komponenten)", computer.Hardware.Count);
        }
        catch (Exception ex)
        {
            _computer = null;
            _log.LogWarning(ex, "Sensor-Stack konnte nicht geöffnet werden (Treiber/Adminrechte?)");
        }
    }

    private static SensorKind? Map(SensorType type) => type switch
    {
        SensorType.Temperature => SensorKind.Temperature,
        SensorType.Load => SensorKind.Load,
        SensorType.Fan => SensorKind.FanRpm,
        SensorType.Clock => SensorKind.ClockMhz,
        SensorType.Voltage => SensorKind.Voltage,
        SensorType.Power => SensorKind.Power,
        SensorType.Level => SensorKind.Level,
        SensorType.Data => SensorKind.Data,
        _ => null
    };

    public void Dispose()
    {
        lock (_gate)
        {
            try { _computer?.Close(); }
            catch { /* best effort */ }
            _computer = null;
        }
    }

    /// <summary>Aktualisiert Hardware inkl. Sub-Hardware vor dem Auslesen.</summary>
    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
