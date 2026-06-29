using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Core.StressTest;

/// <summary>Min/Ø/Max eines Sensors über den gesamten Stresstest.</summary>
public sealed record SensorStat(
    string Hardware,
    string Name,
    SensorKind Kind,
    double Min,
    double Max,
    double Avg);

/// <summary>
/// Auswertung eines Stresstests: aggregierte Sensorstatistiken plus Bewertung
/// von Thermik/Drosselung/Stabilität.
/// </summary>
public sealed class StressTestReport
{
    public TimeSpan Duration { get; init; }
    public int SampleCount { get; init; }

    public IReadOnlyList<SensorStat> Temperatures { get; init; } = Array.Empty<SensorStat>();
    public IReadOnlyList<SensorStat> Fans { get; init; } = Array.Empty<SensorStat>();
    public IReadOnlyList<SensorStat> Loads { get; init; } = Array.Empty<SensorStat>();
    public IReadOnlyList<SensorStat> Clocks { get; init; } = Array.Empty<SensorStat>();

    public double? MaxCpuTempC { get; init; }
    public double? MaxGpuTempC { get; init; }
    public double? MaxFanRpm { get; init; }

    public bool ThermalThrottlingSuspected { get; init; }
    public string ThrottlingNote { get; init; } = string.Empty;

    public bool Stable { get; init; } = true;
    public string StabilityNote { get; init; } = string.Empty;

    /// <summary>Gesamt-Ampel des Stresstests.</summary>
    public Severity Severity { get; init; } = Severity.Ok;

    /// <summary>Kurzfazit für die UI/den Bericht.</summary>
    public string Verdict { get; init; } = string.Empty;
}
