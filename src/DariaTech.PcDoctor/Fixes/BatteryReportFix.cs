using System.IO;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Erstellt den ausführlichen Windows-Akkubericht (<c>powercfg /batteryreport</c>)
/// mit Ladezyklen und Verschleißverlauf und legt ihn auf dem Desktop ab.
/// Unkritisch (nur Lesen/Report).
/// </summary>
public sealed class BatteryReportFix : IFixAction
{
    public string Title => "Akku-Bericht erstellen";
    public string Description =>
        "Erstellt den ausführlichen Windows-Akkubericht (Ladezyklen, Verschleiß, " +
        "Nutzungsverlauf) und legt ihn als HTML-Datei auf dem Desktop ab.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var path = Path.Combine(desktop, $"DariaTech-Akkubericht_{DateTime.Now:yyyy-MM-dd_HHmm}.html");

        progress.Report("Erstelle Akku-Bericht …");
        var result = await ProcessRunner.RunAsync(
            "powercfg.exe", $"/batteryreport /output \"{path}\"", progress, ct).ConfigureAwait(false);

        if (result.ExitCode == 0 && File.Exists(path))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { /* Öffnen optional */ }
            return new FixOutcome(true, $"Akku-Bericht gespeichert: {path}");
        }

        return new FixOutcome(false,
            "Akku-Bericht konnte nicht erstellt werden (kein Akku vorhanden?).");
    }
}
