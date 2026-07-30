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
        var problems = new List<string>();

        try
        {
            foreach (var svc in Services)
            {
                progress.Report($"Stoppe Dienst {svc} …");
                await ProcessRunner.RunAsync("net.exe", $"stop {svc}", progress, ct).ConfigureAwait(false);
            }

            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (!RenameIfExists(Path.Combine(windows, "SoftwareDistribution"),
                    Path.Combine(windows, $"SoftwareDistribution.old_{stamp}"), progress, out var e1))
                problems.Add($"SoftwareDistribution: {e1}");

            if (!RenameIfExists(Path.Combine(windows, "System32", "catroot2"),
                    Path.Combine(windows, "System32", $"catroot2.old_{stamp}"), progress, out var e2))
                problems.Add($"catroot2: {e2}");
        }
        finally
        {
            // Die Dienste MÜSSEN wieder laufen – auch bei Abbruch oder Fehler.
            // Ohne wuauserv/bits/cryptsvc funktioniert Windows Update überhaupt
            // nicht mehr; ein halb erledigter Vorgang wäre schlimmer als keiner.
            // Deshalb bewusst ohne CancellationToken.
            foreach (var svc in Services)
            {
                progress.Report($"Starte Dienst {svc} …");
                try
                {
                    var start = await ProcessRunner
                        .RunAsync("net.exe", $"start {svc}", progress, CancellationToken.None)
                        .ConfigureAwait(false);

                    // 0 = gestartet, 2 = läuft bereits/ist bereits gestartet.
                    if (start.ExitCode is not (0 or 2))
                        problems.Add($"Dienst {svc} startete nicht (Code {start.ExitCode})");
                }
                catch (Exception ex)
                {
                    problems.Add($"Dienst {svc} startete nicht ({ex.Message})");
                }
            }
        }

        if (problems.Count > 0)
        {
            var failure =
                "Die Update-Komponenten wurden nur teilweise zurückgesetzt: " +
                string.Join("; ", problems) + ". " +
                "Bitte den PC neu starten und die Reparatur erneut ausführen. Läuft ein Dienst " +
                "nicht, hilft: Windows-Suche → „Dienste“ → wuauserv/bits/cryptsvc → „Starten“.";
            progress.Report(failure);
            return new FixOutcome(false, failure);
        }

        const string ok = "Update-Komponenten zurückgesetzt, alle Dienste laufen wieder. " +
                          "Bitte erneut nach Updates suchen.";
        progress.Report(ok);
        return new FixOutcome(true, ok);
    }

    /// <summary>
    /// Benennt einen Ordner um, sofern vorhanden. Liefert <c>false</c> mit Grund,
    /// wenn das fehlschlug – ein stillschweigend übergangener Fehler würde als
    /// Erfolg gemeldet, obwohl nichts passiert ist.
    /// </summary>
    private static bool RenameIfExists(string path, string target, IProgress<string> progress,
        out string? error)
    {
        error = null;
        try
        {
            if (!Directory.Exists(path)) return true;   // nichts zu tun

            progress.Report($"Benenne um: {Path.GetFileName(path)} → {Path.GetFileName(target)}");
            Directory.Move(path, target);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            progress.Report($"Konnte {Path.GetFileName(path)} nicht umbenennen: {ex.Message}");
            return false;
        }
    }
}
