using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Core.StressTest;

/// <summary>
/// Wertet die gesammelten Sensor-Samples eines Stresstests aus (rein funktional,
/// daher gut testbar): Min/Ø/Max je Sensor, Spitzentemperaturen, Erkennung von
/// thermischer/Takt-Drosselung und Gesamtbewertung.
/// </summary>
public static class StressTestAnalyzer
{
    public static StressTestReport Analyze(
        IReadOnlyList<StressSample> samples,
        StressTestOptions options,
        bool workerFaulted = false,
        string? faultNote = null)
    {
        var duration = samples.Count > 0 ? samples[^1].Elapsed : TimeSpan.Zero;
        var stable = !workerFaulted;
        var stabilityNote = workerFaulted
            ? (faultNote ?? "Während des Tests trat ein Fehler auf.")
            : "Keine Fehler/Abstürze während des Tests.";

        if (samples.Count == 0)
        {
            return new StressTestReport
            {
                Duration = duration,
                SampleCount = 0,
                Stable = stable,
                StabilityNote = stabilityNote,
                Severity = stable ? Severity.Info : Severity.Critical,
                Verdict = stable
                    ? "Keine Sensordaten erfasst (Sensorik nicht verfügbar?)."
                    : "Test instabil – siehe Stabilitätshinweis."
            };
        }

        var stats = Aggregate(samples);

        var temps = stats.Where(s => s.Kind == SensorKind.Temperature).ToList();
        var fans = stats.Where(s => s.Kind == SensorKind.FanRpm).ToList();
        var loads = stats.Where(s => s.Kind == SensorKind.Load).ToList();
        var clocks = stats.Where(s => s.Kind == SensorKind.ClockMhz).ToList();

        double? maxCpuTemp = MaxValue(samples, SensorKind.Temperature, IsCpu);
        double? maxGpuTemp = MaxValue(samples, SensorKind.Temperature, IsGpu);
        double? maxFan = MaxValue(samples, SensorKind.FanRpm, _ => true);

        var (clockThrottle, clockNote) = DetectClockThrottling(samples);
        var thermalThrottle =
            (maxCpuTemp is double ct && ct >= options.CpuTempCriticalC) ||
            (maxGpuTemp is double gt && gt >= options.GpuTempCriticalC);

        var throttling = thermalThrottle || clockThrottle;
        var throttleNote = BuildThrottleNote(thermalThrottle, clockThrottle, clockNote, maxCpuTemp, maxGpuTemp, options);

        var severity = DetermineSeverity(stable, throttling, maxCpuTemp, options);
        var verdict = BuildVerdict(severity, stable, throttling, maxCpuTemp, maxGpuTemp, maxFan);

        return new StressTestReport
        {
            Duration = duration,
            SampleCount = samples.Count,
            Temperatures = temps,
            Fans = fans,
            Loads = loads,
            Clocks = clocks,
            MaxCpuTempC = maxCpuTemp,
            MaxGpuTempC = maxGpuTemp,
            MaxFanRpm = maxFan,
            ThermalThrottlingSuspected = throttling,
            ThrottlingNote = throttleNote,
            Stable = stable,
            StabilityNote = stabilityNote,
            Severity = severity,
            Verdict = verdict
        };
    }

    private static List<SensorStat> Aggregate(IReadOnlyList<StressSample> samples)
    {
        var groups = new Dictionary<(string Hw, string Name, SensorKind Kind), (double Min, double Max, double Sum, int Count)>();

        foreach (var sample in samples)
        foreach (var r in sample.Readings)
        {
            var key = (r.HardwareName, r.Name, r.Kind);
            if (groups.TryGetValue(key, out var acc))
                groups[key] = (Math.Min(acc.Min, r.Value), Math.Max(acc.Max, r.Value), acc.Sum + r.Value, acc.Count + 1);
            else
                groups[key] = (r.Value, r.Value, r.Value, 1);
        }

        return groups
            .Select(kv => new SensorStat(kv.Key.Hw, kv.Key.Name, kv.Key.Kind,
                Math.Round(kv.Value.Min, 1), Math.Round(kv.Value.Max, 1),
                Math.Round(kv.Value.Sum / kv.Value.Count, 1)))
            .OrderBy(s => s.Kind).ThenBy(s => s.Hardware).ThenBy(s => s.Name)
            .ToList();
    }

    private static double? MaxValue(
        IReadOnlyList<StressSample> samples, SensorKind kind, Func<SensorReading, bool> filter)
    {
        double? max = null;
        foreach (var sample in samples)
        foreach (var r in sample.Readings)
        {
            if (r.Kind != kind || !filter(r)) continue;
            if (max is null || r.Value > max) max = r.Value;
        }
        return max is double d ? Math.Round(d, 1) : null;
    }

    /// <summary>
    /// Takt-Drosselung: bleibt die CPU-Last hoch, fällt der CPU-Takt im letzten
    /// Viertel des Tests aber deutlich unter den beobachteten Spitzentakt, deutet
    /// das auf Throttling hin.
    /// </summary>
    private static (bool Throttle, string Note) DetectClockThrottling(IReadOnlyList<StressSample> samples)
    {
        var clockSeries = samples
            .Select(s => MaxIn(s, SensorKind.ClockMhz, IsCpu))
            .Where(v => v is double).Select(v => v!.Value).ToList();
        var loadSeries = samples
            .Select(s => MaxIn(s, SensorKind.Load, IsCpu))
            .Where(v => v is double).Select(v => v!.Value).ToList();

        if (clockSeries.Count < 4 || loadSeries.Count < 4)
            return (false, string.Empty);

        var peakClock = clockSeries.Max();
        var lastQuarter = Math.Max(1, clockSeries.Count / 4);
        var lateClockAvg = clockSeries.Skip(clockSeries.Count - lastQuarter).Average();
        var lateLoadAvg = loadSeries.Skip(loadSeries.Count - Math.Max(1, loadSeries.Count / 4)).Average();

        if (peakClock > 0 && lateLoadAvg >= 80 && lateClockAvg < 0.90 * peakClock)
        {
            var dropPct = (int)Math.Round((1 - lateClockAvg / peakClock) * 100);
            return (true, $"CPU-Takt fiel unter Dauerlast um ca. {dropPct}% (von {peakClock:N0} auf {lateClockAvg:N0} MHz).");
        }
        return (false, string.Empty);
    }

    private static double? MaxIn(StressSample sample, SensorKind kind, Func<SensorReading, bool> filter)
    {
        double? max = null;
        foreach (var r in sample.Readings)
        {
            if (r.Kind != kind || !filter(r)) continue;
            if (max is null || r.Value > max) max = r.Value;
        }
        return max;
    }

    private static string BuildThrottleNote(
        bool thermal, bool clock, string clockNote,
        double? maxCpuTemp, double? maxGpuTemp, StressTestOptions options)
    {
        if (!thermal && !clock)
            return "Keine Anzeichen von Drosselung – Kühlung hält die Taktraten.";

        var parts = new List<string>();
        if (thermal)
        {
            if (maxCpuTemp is double ct && ct >= options.CpuTempCriticalC)
                parts.Add($"CPU erreichte {ct:N0} °C (Limit ~{options.CpuTempCriticalC:N0} °C).");
            if (maxGpuTemp is double gt && gt >= options.GpuTempCriticalC)
                parts.Add($"GPU erreichte {gt:N0} °C (Limit ~{options.GpuTempCriticalC:N0} °C).");
        }
        if (clock && !string.IsNullOrEmpty(clockNote))
            parts.Add(clockNote);

        return "Drosselung wahrscheinlich: " + string.Join(" ", parts) +
               " Kühlung/Reinigung oder bessere Belüftung prüfen.";
    }

    private static Severity DetermineSeverity(
        bool stable, bool throttling, double? maxCpuTemp, StressTestOptions options)
    {
        if (!stable) return Severity.Critical;
        if (maxCpuTemp is double ct && ct >= options.CpuTempCriticalC) return Severity.Critical;
        if (throttling) return Severity.Warning;
        if (maxCpuTemp is double w && w >= options.CpuTempWarnC) return Severity.Warning;
        return Severity.Ok;
    }

    private static string BuildVerdict(
        Severity severity, bool stable, bool throttling,
        double? maxCpuTemp, double? maxGpuTemp, double? maxFan)
    {
        if (!stable) return "Instabil – der PC hat den Test nicht fehlerfrei überstanden.";

        var temp = maxCpuTemp is double ct ? $"CPU max {ct:N0} °C" : "CPU-Temp n/v";
        var gpu = maxGpuTemp is double gt ? $", GPU max {gt:N0} °C" : "";
        var fan = maxFan is double f ? $", Lüfter max {f:N0} RPM" : "";

        return severity switch
        {
            Severity.Critical => $"Kritisch: zu heiß ({temp}{gpu}{fan}). Kühlung dringend prüfen.",
            Severity.Warning => $"Stabil, aber grenzwertig ({temp}{gpu}{fan})" +
                                (throttling ? " mit Drosselung." : "."),
            _ => $"Bestanden: stabil und thermisch im grünen Bereich ({temp}{gpu}{fan})."
        };
    }

    private static bool IsCpu(SensorReading r)
        => string.Equals(r.HardwareType, "Cpu", StringComparison.OrdinalIgnoreCase);

    private static bool IsGpu(SensorReading r)
        => r.HardwareType.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase);
}
