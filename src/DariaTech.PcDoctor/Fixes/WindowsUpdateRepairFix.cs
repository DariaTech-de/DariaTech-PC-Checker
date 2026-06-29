using System.IO;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Repariert die Windows-Update-Komponenten: stoppt die Dienste
/// (<c>wuauserv</c>, <c>bits</c>, <c>cryptsvc</c>), benennt die Ordner
/// <c>SoftwareDistribution</c> und <c>catroot2</c> um (Windows legt sie neu an)
/// und startet die Dienste wieder. Vorher Wiederherstellungspunkt + Bestätigung.
/// </summary>
public sealed class WindowsUpdateRepairFix : IFixAction
{
    private static readonly string[] Services = { "wuauserv", "bits", "cryptsvc" };

    public string Title => "Windows-Update reparieren";
    public string Description =>
        "Setzt die Update-Komponenten zurück: stoppt die Update-Dienste, benennt " +
        "die Ordner SoftwareDistribution und catroot2 um (Windows erstellt sie neu) " +
        "und startet die Dienste wieder. Hilft bei festhängenden Updates.";
    public bool RequiresRestorePoint => true;
    public bool IsReversible => false;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        foreach (var svc in Services)
        {
            progress.Report($"Stoppe Dienst {svc} …");
            await ProcessRunner.RunAsync("net.exe", $"stop {svc}", progress, ct).ConfigureAwait(false);
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        RenameIfExists(Path.Combine(windows, "SoftwareDistribution"),
            Path.Combine(windows, $"SoftwareDistribution.old_{stamp}"), progress);
        RenameIfExists(Path.Combine(windows, "System32", "catroot2"),
            Path.Combine(windows, "System32", $"catroot2.old_{stamp}"), progress);

        foreach (var svc in Services)
        {
            ct.ThrowIfCancellationRequested();
            progress.Report($"Starte Dienst {svc} …");
            await ProcessRunner.RunAsync("net.exe", $"start {svc}", progress, ct).ConfigureAwait(false);
        }

        return new FixOutcome(true,
            "Update-Komponenten zurückgesetzt. Bitte erneut nach Updates suchen.");
    }

    private static void RenameIfExists(string path, string target, IProgress<string> progress)
    {
        try
        {
            if (Directory.Exists(path))
            {
                progress.Report($"Benenne um: {Path.GetFileName(path)} → {Path.GetFileName(target)}");
                Directory.Move(path, target);
            }
        }
        catch (Exception ex)
        {
            progress.Report($"Konnte {Path.GetFileName(path)} nicht umbenennen: {ex.Message}");
        }
    }
}
