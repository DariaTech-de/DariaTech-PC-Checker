using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Vollständiger Virenscan mit Microsoft Defender
/// (<c>MpCmdRun.exe -Scan -ScanType 2</c>). Prüft alle Dateien auf allen
/// Laufwerken. Nach dem Scan wird die Bedrohungshistorie ausgelesen, damit das
/// Ergebnis konkret benannt werden kann statt nur „fertig“.
/// </summary>
public sealed class DefenderFullScanFix : IFixAction
{
    public string Title => "Defender-Vollscan (alle Dateien)";

    public string Description =>
        "Prüft mit Microsoft Defender ALLE Dateien auf allen Laufwerken auf Viren, Trojaner und " +
        "sonstige Schadsoftware. Gefundene Bedrohungen werden von Defender automatisch behandelt " +
        "(bereinigt oder in Quarantäne verschoben).\n\n" +
        "Dauer: je nach Datenmenge 30 Minuten bis mehrere Stunden. Der PC kann während des Scans " +
        "langsamer reagieren; er lässt sich jederzeit abbrechen. Tipp: vorher „Virensignaturen " +
        "aktualisieren“ ausführen.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;   // Funde werden bereinigt/in Quarantäne verschoben

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        var mpCmdRun = DefenderCli.FindMpCmdRun();
        if (mpCmdRun is null) return new FixOutcome(false, DefenderCli.NotFoundMessage);

        // Ausgangslage festhalten, um hinterher NEUE Funde benennen zu können.
        var before = DefenderReader.ReadThreats(ct).Count;

        progress.Report("Starte Defender-Vollscan – das kann längere Zeit dauern …");
        var result = await ProcessRunner.RunAsync(mpCmdRun, "-Scan -ScanType 2", progress, ct)
            .ConfigureAwait(false);

        var threats = DefenderReader.ReadThreats(ct);
        var newFindings = Math.Max(0, threats.Count - before);
        var unresolved = threats.Count(t => ThreatStatusMapper.NeedsAction(t.StatusId));

        // Exitcode 2 bedeutet bei MpCmdRun „Bedrohung gefunden“ – kein Fehler.
        if (result.ExitCode is not (0 or 2))
            return new FixOutcome(false,
                $"Der Scan wurde mit Code {result.ExitCode} beendet. Details stehen im Protokoll.");

        if (unresolved > 0)
            return new FixOutcome(false,
                $"Vollscan abgeschlossen – {newFindings} neue Erkennung(en), aber {unresolved} " +
                "Bedrohung(en) sind noch NICHT beseitigt. Bitte „Erkannte Bedrohungen entfernen“ " +
                "ausführen; bleibt es dabei, den Offlinescan nutzen.");

        var msg = newFindings > 0
            ? $"Vollscan abgeschlossen – {newFindings} Bedrohung(en) gefunden und von Defender " +
              "behandelt (bereinigt bzw. in Quarantäne). Keine offenen Funde."
            : "Vollscan abgeschlossen – keine Schadsoftware gefunden.";

        progress.Report(msg);
        return new FixOutcome(true, msg);
    }
}
