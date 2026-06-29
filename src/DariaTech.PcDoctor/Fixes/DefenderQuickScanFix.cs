using System.IO;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Startet einen Microsoft-Defender-Schnellscan über
/// <c>MpCmdRun.exe -Scan -ScanType 1</c>.
/// </summary>
public sealed class DefenderQuickScanFix : IFixAction
{
    private static readonly string MpCmdRun = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Windows Defender", "MpCmdRun.exe");

    public string Title => "Defender-Schnellscan starten";
    public string Description =>
        "Startet einen schnellen Virenscan mit Microsoft Defender.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        if (!File.Exists(MpCmdRun))
            return new FixOutcome(false,
                "Microsoft Defender (MpCmdRun.exe) wurde nicht gefunden – evtl. Drittanbieter-AV.");

        progress.Report("Starte Defender-Schnellscan …");
        var result = await ProcessRunner.RunAsync(MpCmdRun, "-Scan -ScanType 1", progress, ct)
            .ConfigureAwait(false);

        return result.ExitCode == 0
            ? new FixOutcome(true, "Schnellscan abgeschlossen – keine Bedrohungen oder bereinigt.")
            : new FixOutcome(false, $"Scan beendet mit Code {result.ExitCode}. Details im Protokoll.");
    }
}
