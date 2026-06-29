using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.StressTest;
using DariaTech.PcDoctor.Models;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class StressTestAnalyzerTests
{
    private static SensorReading Cpu(SensorKind kind, double value, string name = "Core")
        => new("CPU", "Cpu", name, kind, value);

    private static SensorReading Gpu(SensorKind kind, double value, string name = "GPU Core")
        => new("GPU", "GpuNvidia", name, kind, value);

    private static StressSample Sample(int sec, params SensorReading[] readings)
        => new(TimeSpan.FromSeconds(sec), readings);

    private static readonly StressTestOptions Opt = new();

    [Fact]
    public void Analyze_NoSamples_IsInfoAndStable()
    {
        var report = StressTestAnalyzer.Analyze(new List<StressSample>(), Opt);
        Assert.Equal(0, report.SampleCount);
        Assert.True(report.Stable);
        Assert.Equal(Severity.Info, report.Severity);
    }

    [Fact]
    public void Analyze_CoolAndStable_IsOk()
    {
        var samples = new List<StressSample>();
        for (var i = 0; i < 10; i++)
            samples.Add(Sample(i,
                Cpu(SensorKind.Temperature, 70),
                Cpu(SensorKind.Load, 100),
                Cpu(SensorKind.ClockMhz, 4200),
                Gpu(SensorKind.Temperature, 65),
                Cpu(SensorKind.FanRpm, 1500)));

        var report = StressTestAnalyzer.Analyze(samples, Opt);

        Assert.Equal(Severity.Ok, report.Severity);
        Assert.False(report.ThermalThrottlingSuspected);
        Assert.Equal(70, report.MaxCpuTempC);
        Assert.Equal(65, report.MaxGpuTempC);
        Assert.Equal(1500, report.MaxFanRpm);
    }

    [Fact]
    public void Analyze_OverheatingCpu_IsCriticalWithThrottling()
    {
        var samples = new List<StressSample>();
        for (var i = 0; i < 10; i++)
            samples.Add(Sample(i,
                Cpu(SensorKind.Temperature, 97),
                Cpu(SensorKind.Load, 100)));

        var report = StressTestAnalyzer.Analyze(samples, Opt);

        Assert.Equal(Severity.Critical, report.Severity);
        Assert.True(report.ThermalThrottlingSuspected);
        Assert.Equal(97, report.MaxCpuTempC);
    }

    [Fact]
    public void Analyze_ClockDropsUnderSustainedLoad_DetectsThrottling()
    {
        var samples = new List<StressSample>();
        // Erste Hälfte: hoher Takt; zweite Hälfte: Takt bricht ein, Last bleibt hoch.
        for (var i = 0; i < 8; i++)
            samples.Add(Sample(i, Cpu(SensorKind.ClockMhz, 4500), Cpu(SensorKind.Load, 100),
                Cpu(SensorKind.Temperature, 80)));
        for (var i = 8; i < 16; i++)
            samples.Add(Sample(i, Cpu(SensorKind.ClockMhz, 3200), Cpu(SensorKind.Load, 100),
                Cpu(SensorKind.Temperature, 85)));

        var report = StressTestAnalyzer.Analyze(samples, Opt);

        Assert.True(report.ThermalThrottlingSuspected);
        Assert.Equal(Severity.Warning, report.Severity);
    }

    [Fact]
    public void Analyze_WorkerFaulted_IsCriticalAndUnstable()
    {
        var samples = new List<StressSample> { Sample(0, Cpu(SensorKind.Temperature, 60)) };

        var report = StressTestAnalyzer.Analyze(samples, Opt, workerFaulted: true, faultNote: "Absturz");

        Assert.False(report.Stable);
        Assert.Equal(Severity.Critical, report.Severity);
        Assert.Contains("Absturz", report.StabilityNote);
    }

    [Fact]
    public void Analyze_SafetyAborted_IsCriticalWithSafetyNote()
    {
        var samples = new List<StressSample>
        {
            Sample(0, Cpu(SensorKind.Temperature, 99), Cpu(SensorKind.Load, 100))
        };

        var report = StressTestAnalyzer.Analyze(samples, Opt,
            safetyAborted: true, safetyNote: "CPU erreichte 99 °C (Sicherheitsgrenze 98 °C).");

        Assert.True(report.SafetyAborted);
        Assert.Equal(Severity.Critical, report.Severity);
        Assert.True(report.ThermalThrottlingSuspected);
        Assert.Contains("Sicherheitsabschaltung", report.Verdict);
        Assert.Contains("99", report.SafetyNote);
    }

    [Fact]
    public void Analyze_AggregatesMinMaxAvgPerSensor()
    {
        var samples = new List<StressSample>
        {
            Sample(0, Cpu(SensorKind.Temperature, 60)),
            Sample(1, Cpu(SensorKind.Temperature, 80)),
            Sample(2, Cpu(SensorKind.Temperature, 70))
        };

        var report = StressTestAnalyzer.Analyze(samples, Opt);

        var stat = Assert.Single(report.Temperatures);
        Assert.Equal(60, stat.Min);
        Assert.Equal(80, stat.Max);
        Assert.Equal(70, stat.Avg);
    }
}
