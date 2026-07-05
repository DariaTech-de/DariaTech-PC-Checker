using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.StressTest;
using DariaTech.PcDoctor.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

/// <summary>
/// Verhaltenstests für den Stresstest-Ablauf: Der Test muss auch dann sauber
/// laufen und enden, wenn die Sensorik langsam ist, hängt oder gar nicht
/// antwortet — die Lasterzeugung darf davon nie abhängen.
/// </summary>
public class StressTestServiceTests
{
    private static StressTestService Create(ISensorService sensors)
        => new(sensors, NullLogger<StressTestService>.Instance);

    private static StressTestOptions QuickOptions(bool cpu = false, bool memory = false) => new()
    {
        Duration = TimeSpan.FromMilliseconds(400),
        SampleInterval = TimeSpan.FromMilliseconds(50),
        SensorReadTimeout = TimeSpan.FromMilliseconds(100),
        StressCpu = cpu,
        StressMemory = memory,
        MemoryMegabytes = 8
    };

    [Fact]
    public async Task RunAsync_HealthySensors_CollectsSamplesAndFinishes()
    {
        var service = Create(new FakeSensorService(new SensorReading("CPU", "Cpu", "Core", SensorKind.Temperature, 60)));
        var report = await service.RunAsync(QuickOptions());

        Assert.True(report.SampleCount > 1);
        Assert.True(report.Stable);
        Assert.False(report.SafetyAborted);
        Assert.Equal(60, report.MaxCpuTempC);
    }

    [Fact]
    public async Task RunAsync_WithCpuAndMemoryLoad_FinishesWithinDuration()
    {
        var service = Create(new FakeSensorService(new SensorReading("CPU", "Cpu", "Core", SensorKind.Temperature, 55)));

        // Muss trotz voll ausgelasteter Kerne zeitnah nach Ablauf enden —
        // insbesondere dürfen die Last-Threads das Sampling nicht aushungern.
        var run = service.RunAsync(QuickOptions(cpu: true, memory: true));
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.Same(run, finished);
        var report = await run;
        Assert.True(report.SampleCount >= 1);
        Assert.True(report.Stable);
    }

    [Fact]
    public async Task RunAsync_HangingSensors_StillFinishesAndReportsNoData()
    {
        using var hang = new ManualResetEventSlim(false);
        var service = Create(new HangingSensorService(hang));

        var run = service.RunAsync(QuickOptions());
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.Same(run, finished);
        var report = await run;
        Assert.True(report.SampleCount >= 1);          // Samples ohne Sensordaten
        Assert.Null(report.MaxCpuTempC);
        hang.Set();                                     // hängenden Read freigeben
    }

    [Fact]
    public async Task RunAsync_CriticalCpuTemperature_TriggersSafetyAbort()
    {
        var service = Create(new FakeSensorService(new SensorReading("CPU", "Cpu", "Core", SensorKind.Temperature, 99)));
        var options = new StressTestOptions
        {
            Duration = TimeSpan.FromSeconds(30),
            SampleInterval = TimeSpan.FromMilliseconds(50),
            SensorReadTimeout = TimeSpan.FromMilliseconds(500),
            StressCpu = false,
            StressMemory = false
        };

        var run = service.RunAsync(options);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.Same(run, finished);                     // sofortiger Abbruch, nicht erst nach 30 s
        var report = await run;
        Assert.True(report.SafetyAborted);
        Assert.Contains("Sicherheitsabschaltung", report.Verdict);
    }

    [Fact]
    public async Task RunAsync_Cancelled_ReturnsReportWithCollectedSamples()
    {
        var service = Create(new FakeSensorService(new SensorReading("CPU", "Cpu", "Core", SensorKind.Temperature, 60)));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var options = new StressTestOptions
        {
            Duration = TimeSpan.FromSeconds(30),
            SampleInterval = TimeSpan.FromMilliseconds(50),
            SensorReadTimeout = TimeSpan.FromMilliseconds(500),
            StressCpu = false,
            StressMemory = false
        };

        var report = await service.RunAsync(options, progress: null, ct: cts.Token);
        Assert.True(report.SampleCount >= 1);
    }

    private sealed class FakeSensorService : ISensorService
    {
        private readonly SensorReading[] _readings;
        public FakeSensorService(params SensorReading[] readings) => _readings = readings;
        public bool IsAvailable => true;
        public IReadOnlyList<SensorReading> Read() => _readings;
        public void Dispose() { }
    }

    /// <summary>Simuliert einen hängenden Sensor-Stack (Read blockiert).</summary>
    private sealed class HangingSensorService : ISensorService
    {
        private readonly ManualResetEventSlim _gate;
        public HangingSensorService(ManualResetEventSlim gate) => _gate = gate;
        public bool IsAvailable => true;
        public IReadOnlyList<SensorReading> Read()
        {
            _gate.Wait();
            return Array.Empty<SensorReading>();
        }
        public void Dispose() { }
    }
}
