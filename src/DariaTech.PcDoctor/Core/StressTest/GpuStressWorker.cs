using ILGPU;
using ILGPU.Runtime;

namespace DariaTech.PcDoctor.Core.StressTest;

/// <summary>
/// Erzeugt echte GPU-Last für den Stresstest: kompiliert per ILGPU einen kleinen
/// Rechen-Kernel und lässt ihn in Dauerschleife auf der Grafikkarte laufen
/// (CUDA bei NVIDIA, sonst OpenCL). Damit steigen GPU-Temperatur und -Lüfter unter
/// Last – anders als bei reiner CPU-/RAM-Belastung.
///
/// Alles ist gekapselt und best-effort: Findet sich keine unterstützte GPU oder
/// scheitert die Initialisierung/JIT, wird die GPU-Last sauber übersprungen. Ein
/// Fehler hier darf den Stresstest nie als „instabil" erscheinen lassen.
/// </summary>
internal sealed class GpuStressWorker : IDisposable
{
    private const int Elements = 1 << 20;   // ~1 Mio. parallele Threads
    private const int InnerIterations = 100_000;

    private Context? _context;
    private Accelerator? _accelerator;
    private MemoryBuffer1D<float, Stride1D.Dense>? _buffer;
    private Action<Index1D, ArrayView1D<float, Stride1D.Dense>, int>? _kernel;

    /// <summary>
    /// Initialisiert eine GPU (keine CPU-Fallback-„GPU"). Liefert bei Erfolg true
    /// und den GPU-Namen in <paramref name="description"/>, sonst false + Grund.
    /// </summary>
    public bool TryInitialize(out string description)
    {
        try
        {
            _context = Context.CreateDefault();

            Device? gpu = null;
            foreach (var device in _context.Devices)
            {
                if (device.AcceleratorType != AcceleratorType.CPU) { gpu = device; break; }
            }

            if (gpu is null)
            {
                description = "keine unterstützte GPU gefunden";
                Dispose();
                return false;
            }

            _accelerator = gpu.CreateAccelerator(_context);
            _buffer = _accelerator.Allocate1D<float>(Elements);
            _kernel = _accelerator.LoadAutoGroupedStreamKernel<
                Index1D, ArrayView1D<float, Stride1D.Dense>, int>(GpuKernel);

            description = _accelerator.Name;
            return true;
        }
        catch (Exception ex)
        {
            description = ex.Message;
            Dispose();
            return false;
        }
    }

    /// <summary>Belastet die GPU, bis abgebrochen wird. Muss nach erfolgreichem TryInitialize laufen.</summary>
    public void Run(CancellationToken token)
    {
        if (_accelerator is null || _buffer is null || _kernel is null) return;

        while (!token.IsCancellationRequested)
        {
            _kernel(_buffer.IntExtent, _buffer.View, InnerIterations);
            _accelerator.Synchronize();   // hält den Takt und die Abbruchprüfung reaktionsfähig
        }
    }

    /// <summary>Reine Gleitkomma-Rechenlast (FMA-Schleife); Ergebnis wird zurückgeschrieben, damit nichts wegoptimiert wird.</summary>
    private static void GpuKernel(Index1D index, ArrayView1D<float, Stride1D.Dense> data, int iterations)
    {
        float v = 1.0f + (index.X % 97) * 0.001f;
        for (var i = 0; i < iterations; i++)
        {
            v = v * 1.0000001f + 0.5f;
            if (v > 1e30f) v = 1.0f;   // Overflow vermeiden, Last konstant halten
        }
        data[index] = v;
    }

    public void Dispose()
    {
        try { _buffer?.Dispose(); } catch { /* best effort */ }
        try { _accelerator?.Dispose(); } catch { /* best effort */ }
        try { _context?.Dispose(); } catch { /* best effort */ }
        _buffer = null;
        _accelerator = null;
        _context = null;
        _kernel = null;
    }
}
