using System.Diagnostics;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>Ergebnis eines ausgeführten Prozesses.</summary>
public sealed record ProcessResult(int ExitCode, string Output);

/// <summary>
/// Startet Konsolen-Tools (sfc, dism, chkdsk, ipconfig …) und streamt deren
/// Ausgabe zeilenweise an einen <see cref="IProgress{T}"/> (Live-Log in der UI).
/// UI-frei.
/// </summary>
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new System.Text.StringBuilder();

        void OnData(object _, DataReceivedEventArgs e)
        {
            if (e.Data is null) return;
            output.AppendLine(e.Data);
            progress?.Report(e.Data);
        }

        process.OutputDataReceived += OnData;
        process.ErrorDataReceived += OnData;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* best effort */ }
            throw;
        }

        return new ProcessResult(process.ExitCode, output.ToString());
    }
}
