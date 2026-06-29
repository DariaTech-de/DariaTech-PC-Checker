using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Liefert Live-Hardwaresensoren (Temperaturen, Lüfter, Takt, Last …).
/// Implementierungen kapseln die konkrete Quelle (z. B. LibreHardwareMonitor)
/// und müssen sauber degradieren, wenn keine Sensoren verfügbar sind
/// (<see cref="IsAvailable"/> = false statt zu werfen).
/// </summary>
public interface ISensorService : IDisposable
{
    /// <summary>True, wenn die Sensorquelle initialisiert werden konnte.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Aktualisiert die Sensoren und gibt eine Momentaufnahme zurück.
    /// Bei nicht verfügbarer Quelle eine leere Liste.
    /// </summary>
    IReadOnlyList<SensorReading> Read();
}
