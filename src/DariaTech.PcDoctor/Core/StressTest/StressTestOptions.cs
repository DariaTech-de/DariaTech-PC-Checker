namespace DariaTech.PcDoctor.Core.StressTest;

/// <summary>Parameter eines Stresstests.</summary>
public sealed class StressTestOptions
{
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(2);
    public bool StressCpu { get; init; } = true;
    public bool StressMemory { get; init; } = true;

    /// <summary>
    /// Zusätzlich die Grafikkarte belasten (wichtig bei Gaming-PCs, damit GPU-Temperatur
    /// und -Lüfter unter Last steigen). Läuft eine unterstützte GPU nicht, wird die
    /// GPU-Last sauber übersprungen – der übrige Test läuft normal weiter.
    /// </summary>
    public bool StressGpu { get; init; } = true;
    public int MemoryMegabytes { get; init; } = 1024;
    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximale Wartezeit auf eine einzelne Sensorabfrage. Antwortet der
    /// Sensor-Stack nicht (hängender Treiber), läuft der Test ohne Live-Werte
    /// weiter, statt einzufrieren.
    /// </summary>
    public TimeSpan SensorReadTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>CPU-Temperatur (°C), ab der von thermischer Drosselung ausgegangen wird.</summary>
    public double CpuTempCriticalC { get; init; } = 95;

    /// <summary>CPU-Temperatur (°C), ab der gewarnt wird (heiß, aber unkritisch).</summary>
    public double CpuTempWarnC { get; init; } = 88;

    /// <summary>GPU-Temperatur (°C), ab der von Drosselung ausgegangen wird.</summary>
    public double GpuTempCriticalC { get; init; } = 90;

    // --- Sicherheit ---

    /// <summary>
    /// Automatische Notabschaltung des Tests, sobald eine Sicherheitstemperatur
    /// erreicht wird (verhindert Hitzeschäden). Standardmäßig aktiv.
    /// </summary>
    public bool EnableThermalSafety { get; init; } = true;

    /// <summary>CPU-Temperatur (°C), bei der der Test sofort abgebrochen wird.</summary>
    public double SafetyCpuTempC { get; init; } = 98;

    /// <summary>GPU-Temperatur (°C), bei der der Test sofort abgebrochen wird.</summary>
    public double SafetyGpuTempC { get; init; } = 95;
}
