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
                return _computer is not null;
            }
        }
    }

    public IReadOnlyList<SensorReading> Read()
    {
        lock (_gate)
        {
            EnsureInitialized();
            if (_computer is null) return Array.Empty<SensorReading>();

            var readings = new List<SensorReading>();
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
            return readings;
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

            into.Add(new SensorReading(
                hardware.Name,
                hardware.HardwareType.ToString(),
                sensor.Name,
                kind.Value,
                value));
        }
    }

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
