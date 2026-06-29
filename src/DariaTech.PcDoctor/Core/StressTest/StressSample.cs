using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Core.StressTest;

/// <summary>Sensor-Momentaufnahme zu einem Zeitpunkt während des Stresstests.</summary>
public sealed record StressSample(TimeSpan Elapsed, IReadOnlyList<SensorReading> Readings);

/// <summary>Fortschrittsmeldung an die UI während des Stresstests.</summary>
public sealed record StressProgress(
    TimeSpan Elapsed,
    TimeSpan Total,
    IReadOnlyList<SensorReading> Current);
