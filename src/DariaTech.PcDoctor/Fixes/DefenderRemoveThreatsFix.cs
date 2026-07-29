using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Beseitigt die von Defender erkannten, noch offenen Bedrohungen über das
/// offizielle PowerShell-Cmdlet <c>Remove-MpThreat</c>. Danach wird die
/// Bedrohungshistorie erneut gelesen, um den Erfolg tatsächlich zu belegen –
/// nicht nur zu behaupten.
/// </summary>
public sealed class DefenderRemoveThreatsFix : IFixAction
{
    public string Title => "Erkannte Bedrohungen entfernen";

    public string Description =>
        "Lässt Microsoft Defender alle aktuell erkannten Bedrohungen beseitigen (bereinigen, in " +
        "Quarantäne verschieben oder löschen). Betroffene Schadprogramme werden dabei entfernt – " +
        "eigene Dokumente sind nicht betroffen.\n\n" +
        "Wichtig: Wurde eine Bedrohung in einer Datei gefunden, die auch Nutzdaten enthält " +
        "(z. B. ein infiziertes Office-Dokument), kann Defender die Datei in Quarantäne verschieben. " +
        "Aus der Quarantäne lässt sie sich bei Bedarf wiederherstellen. " +
        "Nach dem Entfernen prüft die App nach, ob wirklich keine offenen Funde mehr vorliegen.";

    public bool RequiresRestorePoint => false;   // Defender-Quarantäne ist selbst die Rückfallebene
    public bool IsReversible => true;            // Quarantäne erlaubt Wiederherstellung

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        if (DefenderCli.FindMpCmdRun() is null)
            return new FixOutcome(false, DefenderCli.NotFoundMessage);

        var before = DefenderReader.ReadThreats(ct)
            .Count(t => ThreatStatusMapper.NeedsAction(t.StatusId));

        if (before == 0)
        {
            const string nothing = "Es liegen keine offenen Bedrohungen vor – nichts zu entfernen.";
            progress.Report(nothing);
            return new FixOutcome(true, nothing);
        }

        progress.Report($"Entferne {before} offene Bedrohung(en) …");

        // Remove-MpThreat ist der offizielle Weg; Ausgabe wird live gestreamt.
        var result = await ProcessRunner.RunAsync(
            "powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -Command " +
            "\"try { Remove-MpThreat -ErrorAction Stop | Out-String } catch { Write-Output ('FEHLER: ' + $_.Exception.Message) }\"",
            progress, ct).ConfigureAwait(false);

        // Erfolgskontrolle: Historie erneut lesen.
        var after = DefenderReader.ReadThreats(ct)
            .Count(t => ThreatStatusMapper.NeedsAction(t.StatusId));

        if (after == 0)
        {
            var ok = $"{before} Bedrohung(en) beseitigt – es liegen keine offenen Funde mehr vor.";
            progress.Report(ok);
            return new FixOutcome(true, ok);
        }

        var msg = $"Von {before} Bedrohung(en) sind noch {after} offen. " +
                  "Hartnäckige Schädlinge lassen sich im laufenden Windows oft nicht entfernen – " +
                  "bitte „Defender-Offlinescan“ ausführen (startet den PC neu und entfernt vor dem " +
                  "Windows-Start) und zusätzlich den „Microsoft Safety Scanner“ als Zweitmeinung.";
        progress.Report(msg);
        return new FixOutcome(false, msg);
    }
}
