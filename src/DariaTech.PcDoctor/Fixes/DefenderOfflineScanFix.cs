using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Startet den Microsoft-Defender-Offlinescan (<c>Start-MpWDOScan</c>). Der PC
/// startet dabei neu und prüft sich VOR dem Laden von Windows – die einzige
/// zuverlässige Möglichkeit, tief verankerte Schädlinge (Rootkits) zu entfernen,
/// die sich im laufenden System schützen.
///
/// Achtung: Diese Aktion startet den Rechner neu. Deshalb wird sie besonders
/// deutlich beschrieben; die Bestätigung erfolgt wie bei allen Reparaturen vorab
/// durch den Nutzer.
/// </summary>
public sealed class DefenderOfflineScanFix : IFixAction
{
    public string Title => "Defender-Offlinescan (Neustart erforderlich)";

    public string Description =>
        "Für hartnäckige Schadsoftware: Der PC startet NEU und wird vor dem Laden von Windows geprüft. " +
        "Nur so lassen sich Schädlinge entfernen, die sich im laufenden Betrieb selbst schützen " +
        "(Rootkits, Bedrohungen, die nach dem Entfernen wiederkehren).\n\n" +
        "WICHTIG:\n" +
        "• Der Rechner startet unmittelbar neu – bitte vorher ALLE Dateien speichern und Programme schließen.\n" +
        "• Der Scan läuft dann außerhalb von Windows und dauert etwa 15 Minuten.\n" +
        "• Bei Notebooks das Netzteil anschließen.\n" +
        "• Nach dem Neustart stehen die Ergebnisse in der Bedrohungshistorie (Kachel „Schadsoftware-Befunde“).";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;   // Neustart + Bereinigung

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        if (DefenderCli.FindMpCmdRun() is null)
            return new FixOutcome(false, DefenderCli.NotFoundMessage);

        progress.Report("Plane Offlinescan – der PC startet dazu neu …");

        var result = await ProcessRunner.RunAsync(
            "powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -Command " +
            "\"try { Start-MpWDOScan -ErrorAction Stop; Write-Output 'OK' } " +
            "catch { Write-Output ('FEHLER: ' + $_.Exception.Message) }\"",
            progress, ct).ConfigureAwait(false);

        var failed = result.Output.Contains("FEHLER", StringComparison.OrdinalIgnoreCase);
        if (result.ExitCode != 0 || failed)
            return new FixOutcome(false,
                "Der Offlinescan konnte nicht gestartet werden. Alternativ manuell starten: " +
                "Windows-Sicherheit → „Viren- & Bedrohungsschutz“ → „Scanoptionen“ → " +
                "„Microsoft Defender Offlineüberprüfung“ → „Jetzt überprüfen“.");

        const string msg = "Offlinescan geplant – der PC startet jetzt neu und prüft sich vor dem " +
                           "Windows-Start. Ergebnisse danach in der Kachel „Schadsoftware-Befunde“.";
        progress.Report(msg);
        return new FixOutcome(true, msg);
    }
}
