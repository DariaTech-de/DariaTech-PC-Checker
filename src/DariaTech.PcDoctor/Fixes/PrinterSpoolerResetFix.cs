using System.IO;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Setzt den Druckerspooler zurück: Dienst stoppen, hängende Druckaufträge
/// löschen (<c>System32\spool\PRINTERS</c>), Dienst starten. Hilft, wenn der
/// Drucker nicht mehr druckt oder die Warteschlange klemmt.
/// </summary>
public sealed class PrinterSpoolerResetFix : IFixAction
{
    public string Title => "Druckerspooler zurücksetzen";
    public string Description =>
        "Stoppt den Druckdienst, löscht hängende Druckaufträge und startet den " +
        "Dienst neu. Behebt klemmende Druckwarteschlangen.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Stoppe Druckspooler …");
        await ProcessRunner.RunAsync("net.exe", "stop spooler", progress, ct).ConfigureAwait(false);

        var queue = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "PRINTERS");
        var deleted = 0;
        try
        {
            if (Directory.Exists(queue))
                foreach (var file in Directory.EnumerateFiles(queue))
                {
                    ct.ThrowIfCancellationRequested();
                    try { File.Delete(file); deleted++; } catch { /* in Benutzung */ }
                }
        }
        catch { /* Ordner nicht zugreifbar */ }

        progress.Report($"{deleted} hängende(n) Druckauftrag/-aufträge entfernt.");
        progress.Report("Starte Druckspooler …");
        var start = await ProcessRunner.RunAsync("net.exe", "start spooler", progress, ct).ConfigureAwait(false);

        return start.ExitCode == 0
            ? new FixOutcome(true, $"Druckerspooler zurückgesetzt ({deleted} Auftrag/Aufträge entfernt).")
            : new FixOutcome(false, "Spooler-Dienst konnte nicht gestartet werden.");
    }
}
