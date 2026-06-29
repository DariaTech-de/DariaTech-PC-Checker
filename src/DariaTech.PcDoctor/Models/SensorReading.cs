namespace DariaTech.PcDoctor.Models;

/// <summary>Kategorie eines Hardware-Sensorwerts.</summary>
public enum SensorKind
{
    Temperature, // °C
    Load,        // %
    FanRpm,      // RPM
    ClockMhz,    // MHz
    Voltage,     // V
    Power,       // W
    Other
}

/// <summary>
/// Eine einzelne Sensor-Messung (z. B. „CPU Package" = 62 °C). UI-frei.
/// </summary>
/// <param name="HardwareName">Komponente, z. B. „AMD Ryzen 7 5800X".</param>
/// <param name="HardwareType">Grobtyp, z. B. „Cpu", „GpuNvidia", „Motherboard".</param>
/// <param name="Name">Sensorname, z. B. „Core (Tctl/Tdie)".</param>
/// <param name="Kind">Kategorie/Einheit.</param>
/// <param name="Value">Messwert in der zur Kategorie passenden Einheit.</param>
public sealed record SensorReading(
    string HardwareName,
    string HardwareType,
    string Name,
    SensorKind Kind,
    double Value)
{
    public string Unit => Kind switch
    {
        SensorKind.Temperature => "°C",
        SensorKind.Load => "%",
        SensorKind.FanRpm => "RPM",
        SensorKind.ClockMhz => "MHz",
        SensorKind.Voltage => "V",
        SensorKind.Power => "W",
        _ => string.Empty
    };
}
