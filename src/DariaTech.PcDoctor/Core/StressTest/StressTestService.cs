using System.Collections.Concurrent;
using DariaTech.PcDoctor.Models;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.Core.StressTest;

/// <summary>
/// Führt einen Stresstest aus: erzeugt CPU-/RAM-Last, sammelt währenddessen in
/// festem Takt Sensor-Samples und lässt das Ergebnis vom
/// <see cref="StressTestAnalyzer"/> bewerten. Der Test ist jederzeit abbrechbar;
/// auch ein Abbruch liefert einen Bericht über die bis dahin erfassten Daten.
///
/// Wichtig für die Robustheit:
/// - Die Last läuft auf dedizierten Threads, nicht im Threadpool. Spin-Schleifen
///   im Threadpool würden alle Pool-Threads dauerhaft belegen und damit genau
///   die Infrastruktur aushungern (Task.Delay, Sensor-Reads, Progress), die der
///   Test selbst braucht — die Tachos frieren dann ein.
/// - Sensor-Reads laufen mit Timeout. Ein hängender Sensor-Stack (kommt auf
///   manchen Mainboards vor) darf weder den Teststart noch den Abschluss blockieren.
/// </summary>
public sealed class StressTestService
{
    private readonly ISensorService _sensors;
    private readonly ILogger<StressTestService> _log;

    public StressTestService(ISensorService sensors, ILogger<StressTestService> log)
    {
        _sensors = sensors;
        _log = log;
    }

    public async Task<StressTestReport> RunAsync(
        StressTestOptions options,
        IProgress<StressProgress>? progress = null,
        CancellationToken ct = default)
    {
        _log.LogInformation("Stresstest startet: {Duration}, CPU={Cpu}, RAM={Mem}",
            options.Duration, options.StressCpu, options.StressMemory);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(options.Duration);
        var token = cts.Token;

        // Last auf dedizierten Threads erzeugen — unabhängig vom Threadpool.
        var workerErrors = new ConcurrentQueue<Exception>();
        var workers = new List<Thread>();
        if (options.StressCpu)
            for (var i = 0; i < Environment.ProcessorCount; i++)
                workers.Add(StartWorker($"StressCpu-{i}", () => CpuLoad(token), workerErrors));
        if (options.StressMemory)
            workers.Add(StartWorker("StressMemory", () => MemoryLoad(options.MemoryMegabytes, token), workerErrors));
        _log.LogInformation("Stresstest: {Count} Last-Threads gestartet", workers.Count);

        var samples = new List<StressSample>();
        var safetyAborted = false;
        string? safetyNote = null;
        var start = DateTime.UtcNow;
        Task<IReadOnlyList<SensorReading>>? pendingRead = null;
        var sensorTimeoutLogged = false;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var elapsed = DateTime.UtcNow - start;

                // Sensoren mit Zeitlimit lesen: hängt der Sensor-Stack, läuft der
                // Test (inkl. Fortschritt und sauberem Ende) trotzdem weiter.
                pendingRead ??= Task.Run(SafeRead, CancellationToken.None);
                if (!pendingRead.IsCompleted)
                    await Task.WhenAny(pendingRead, Task.Delay(options.SensorReadTimeout, CancellationToken.None))
                        .ConfigureAwait(false);

                IReadOnlyList<SensorReading> readings;
                if (pendingRead.IsCompleted)
                {
                    readings = await pendingRead.ConfigureAwait(false);
                    pendingRead = null;
                }
                else
                {
                    readings = Array.Empty<SensorReading>();
                    if (!sensorTimeoutLogged)
                    {
                        sensorTimeoutLogged = true;
                        _log.LogWarning("Sensorabfrage antwortet nicht (> {Timeout}) – Stresstest läuft ohne Live-Sensorwerte weiter",
                            options.SensorReadTimeout);
                    }
                }

                samples.Add(new StressSample(elapsed, readings));
                progress?.Report(new StressProgress(elapsed, options.Duration, readings));

                // Sicherheit: bei kritischer Temperatur sofort abbrechen.
                if (options.EnableThermalSafety)
                {
                    safetyNote = CheckSafety(readings, options);
                    if (safetyNote is not null)
                    {
                        safetyAborted = true;
                        _log.LogWarning("Stresstest-Notabschaltung: {Reason}", safetyNote);
                        cts.Cancel();
                        break;
                    }
                }

                try { await Task.Delay(options.SampleInterval, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { /* Test beendet/abgebrochen */ }

        var (faulted, note) = await StopWorkersAsync(workers, workerErrors).ConfigureAwait(false);
        _log.LogInformation("Stresstest beendet: {Samples} Samples, stabil={Stable}, Notabschaltung={Safety}",
            samples.Count, !faulted, safetyAborted);

        return StressTestAnalyzer.Analyze(samples, options, faulted, note, safetyAborted, safetyNote);
    }

    /// <summary>Startet einen Last-Thread (Background, damit er das App-Ende nie blockiert).</summary>
    private static Thread StartWorker(string name, Action work, ConcurrentQueue<Exception> errors)
    {
        var thread = new Thread(() =>
        {
            try { work(); }
            catch (OperationCanceledException) { /* erwartetes Ende */ }
            catch (Exception ex) { errors.Enqueue(ex); }
        })
        {
            IsBackground = true,
            Name = name,
            Priority = ThreadPriority.Normal
        };
        thread.Start();
        return thread;
    }

    /// <summary>Liefert einen Abbruchgrund, wenn eine Sicherheitstemperatur erreicht ist – sonst null.</summary>
    private static string? CheckSafety(IReadOnlyList<SensorReading> readings, StressTestOptions options)
    {
        double? cpu = null, gpu = null;
        foreach (var r in readings)
        {
            if (r.Kind != SensorKind.Temperature) continue;
            if (string.Equals(r.HardwareType, "Cpu", StringComparison.OrdinalIgnoreCase))
                cpu = cpu is null ? r.Value : Math.Max(cpu.Value, r.Value);
            else if (r.HardwareType.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase))
                gpu = gpu is null ? r.Value : Math.Max(gpu.Value, r.Value);
        }

        if (cpu is double c && c >= options.SafetyCpuTempC)
            return $"CPU erreichte {c:0} °C (Sicherheitsgrenze {options.SafetyCpuTempC:0} °C).";
        if (gpu is double g && g >= options.SafetyGpuTempC)
            return $"GPU erreichte {g:0} °C (Sicherheitsgrenze {options.SafetyGpuTempC:0} °C).";
        return null;
    }

    private IReadOnlyList<SensorReading> SafeRead()
    {
        try { return _sensors.Read(); }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Sensorabfrage im Stresstest fehlgeschlagen");
            return Array.Empty<SensorReading>();
        }
    }

    private static async Task<(bool Faulted, string? Note)> StopWorkersAsync(
        List<Thread> workers, ConcurrentQueue<Exception> errors)
    {
        if (workers.Count == 0) return (false, null);

        // Threads beenden sich selbst über das Token; hier nur kurz einsammeln,
        // ohne den Aufrufer zu blockieren.
        await Task.Run(() =>
        {
            foreach (var worker in workers)
                worker.Join(TimeSpan.FromSeconds(5));
        }).ConfigureAwait(false);

        if (errors.TryDequeue(out var ex))
            return (true, $"Ein Lastprozess brach unerwartet ab: {ex.Message}");
        return (false, null);
    }

    /// <summary>Hält alle Kerne mit Gleitkomma-Arbeit beschäftigt.</summary>
    private static void CpuLoad(CancellationToken token)
    {
        var x = 1.000001;
        while (!token.IsCancellationRequested)
        {
            // Etwas Rechenarbeit, die der JIT nicht wegoptimiert.
            for (var i = 0; i < 50_000; i++)
                x = Math.Sqrt(x * 1.0000001 + 1.0) + Math.Sin(x);
            if (x == double.PositiveInfinity) x = 1.000001; // Reset, nie erreicht
        }
    }

    /// <summary>Belegt einen Speicherblock und hält die Seiten „warm".</summary>
    private static void MemoryLoad(int megabytes, CancellationToken token)
    {
        var bytes = Math.Max(1, megabytes) * 1024L * 1024L;
        byte[] buffer;
        try { buffer = new byte[Math.Min(bytes, int.MaxValue - 64)]; }
        catch (OutOfMemoryException) { return; } // weniger nehmen statt abstürzen

        var rnd = new Random();
        while (!token.IsCancellationRequested)
        {
            for (var i = 0; i < buffer.Length; i += 4096)
                buffer[i] = (byte)rnd.Next(256);
        }
        GC.KeepAlive(buffer);
    }
}
