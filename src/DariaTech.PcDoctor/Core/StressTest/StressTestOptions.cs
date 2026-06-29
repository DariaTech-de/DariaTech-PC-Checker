namespace DariaTech.PcDoctor.Core.StressTest;

/// <summary>Parameter eines Stresstests.</summary>
public sealed class StressTestOptions
{
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(2);
    public bool StressCpu { get; init; } = true;
    public bool StressMemory { get; init; } = true;
    public int MemoryMegabytes { get; init; } = 1024;
    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>CPU-Temperatur (°C), ab der von thermischer Drosselung ausgegangen wird.</summary>
    public double CpuTempCriticalC { get; init; } = 95;

    /// <summary>CPU-Temperatur (°C), ab der gewarnt wird (heiß, aber unkritisch).</summary>
    public double CpuTempWarnC { get; init; } = 88;

    /// <summary>GPU-Temperatur (°C), ab der von Drosselung ausgegangen wird.</summary>
    public double GpuTempCriticalC { get; init; } = 90;
}
