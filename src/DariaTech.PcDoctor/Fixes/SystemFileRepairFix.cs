using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Repariert beschädigte Systemdateien: zuerst <c>sfc /scannow</c>, danach
/// <c>DISM /Online /Cleanup-Image /RestoreHealth</c>. Die Ausgabe wird live in
/// den Fortschritt gestreamt. Vorher Wiederherstellungspunkt + Bestätigung.
/// </summary>
public sealed class SystemFileRepairFix : IFixAction
{
    public string Title => "Systemdateien reparieren (SFC + DISM)";
    public string Description =>
        "Prüft und repariert beschädigte Windows-Systemdateien mit sfc /scannow " +
        "und DISM RestoreHealth. Kann einige Minuten dauern.";
    public bool RequiresRestorePoint => true;
    public bool IsReversible => false;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Starte sfc /scannow …");
        var sfc = await ProcessRunner.RunAsync("sfc.exe", "/scannow", progress, ct)
            .ConfigureAwait(false);

        progress.Report("Starte DISM /RestoreHealth …");
        var dism = await ProcessRunner.RunAsync(
            "dism.exe", "/Online /Cleanup-Image /RestoreHealth", progress, ct)
            .ConfigureAwait(false);

        var success = sfc.ExitCode == 0 && dism.ExitCode == 0;
        var msg = success
            ? "Systemdateiprüfung abgeschlossen."
            : $"Abgeschlossen mit Hinweisen (SFC-Code {sfc.ExitCode}, DISM-Code {dism.ExitCode}). " +
              "Details im Protokoll.";
        return new FixOutcome(success, msg);
    }
}
