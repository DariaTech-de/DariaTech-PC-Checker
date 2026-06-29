using System.IO;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Prüft das Systemlaufwerk mit <c>chkdsk</c> im schreibgeschützten Modus
/// (Nur-Lesen-Scan, ohne <c>/F</c>). Eine echte Reparatur mit <c>/F</c> würde
/// einen Neustart einplanen und wird hier bewusst nicht automatisch ausgeführt –
/// der Befund weist ggf. darauf hin.
/// </summary>
public sealed class CheckDiskFix : IFixAction
{
    public string Title => "Datenträger prüfen (chkdsk, schreibgeschützt)";
    public string Description =>
        "Führt einen schreibgeschützten Datenträger-Scan des Systemlaufwerks mit " +
        "chkdsk durch (ohne /F, also ohne Reparatur und ohne Neustart). " +
        "Findet der Scan Fehler, sollte chkdsk /F manuell mit Neustart eingeplant werden.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
        progress.Report($"Prüfe {systemDrive} (schreibgeschützt) …");

        var result = await ProcessRunner.RunAsync("chkdsk.exe", systemDrive, progress, ct)
            .ConfigureAwait(false);

        // chkdsk: ExitCode 0 = keine Fehler; sonst Hinweis auf nötige Reparatur.
        return result.ExitCode == 0
            ? new FixOutcome(true, $"{systemDrive} ist fehlerfrei.")
            : new FixOutcome(true,
                $"{systemDrive}: chkdsk meldet Auffälligkeiten (Code {result.ExitCode}). " +
                "Eine Reparatur mit chkdsk /F (Neustart erforderlich) wird empfohlen.");
    }
}
