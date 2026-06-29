using System.IO;
using System.Linq;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Startet den Windows-Explorer neu und baut den Symbol-Cache neu auf. Hilft bei
/// hängender Taskleiste/Explorer und bei kaputten Vorschaubildern/Icons.
/// </summary>
public sealed class RestartExplorerFix : IFixAction
{
    public string Title => "Explorer neu starten & Icon-Cache erneuern";
    public string Description =>
        "Beendet den Windows-Explorer, löscht den Symbol-/Vorschau-Cache und " +
        "startet den Explorer neu. Behebt hängende Taskleiste und kaputte Icons.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Beende Explorer …");
        await ProcessRunner.RunAsync("taskkill.exe", "/f /im explorer.exe", progress, ct).ConfigureAwait(false);

        progress.Report("Lösche Symbol-Cache …");
        var deleted = DeleteIconCache(ct);

        progress.Report("Starte Explorer …");
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true
            });
        }
        catch { /* Explorer startet i. d. R. automatisch wieder */ }

        return new FixOutcome(true,
            $"Explorer neu gestartet, {deleted} Cache-Datei(en) erneuert. Bei Bedarf einmal abmelden/neu anmelden.");
    }

    private static int DeleteIconCache(CancellationToken ct)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var deleted = 0;

        var legacy = Path.Combine(local, "IconCache.db");
        try { if (File.Exists(legacy)) { File.Delete(legacy); deleted++; } } catch { /* gesperrt */ }

        var explorerDir = Path.Combine(local, "Microsoft", "Windows", "Explorer");
        try
        {
            if (Directory.Exists(explorerDir))
                foreach (var file in Directory.EnumerateFiles(explorerDir, "iconcache*.db")
                             .Concat(Directory.EnumerateFiles(explorerDir, "thumbcache*.db")))
                {
                    ct.ThrowIfCancellationRequested();
                    try { File.Delete(file); deleted++; } catch { /* gesperrt */ }
                }
        }
        catch { /* nicht zugreifbar */ }

        return deleted;
    }
}
