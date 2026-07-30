using System.IO;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Schaltet den Systemschutz für das Windows-Laufwerk ein und legt sofort einen
/// ersten Wiederherstellungspunkt an. Damit existiert vor weiteren Reparaturen
/// eine Rückfallebene – ohne Systemschutz laufen auch die automatischen
/// Sicherungspunkte dieser App ins Leere.
///
/// Nach der Aktion wird nachgeprüft, ob wirklich ein Punkt vorhanden ist.
/// </summary>
public sealed class EnableSystemRestoreFix : IFixAction
{
    private readonly RestorePointService _restorePoints;

    public EnableSystemRestoreFix(RestorePointService restorePoints)
        => _restorePoints = restorePoints;

    public string Title => "Systemwiederherstellung einschalten";

    public string Description =>
        "Aktiviert den Windows-Systemschutz für das Systemlaufwerk und legt sofort einen " +
        "Wiederherstellungspunkt an. Damit lässt sich der PC bei Problemen auf den heutigen Stand " +
        "zurücksetzen – die wichtigste Absicherung vor größeren Reparaturen.\n\n" +
        "Windows reserviert dafür etwas Speicherplatz auf dem Systemlaufwerk (üblicherweise wenige " +
        "Prozent). Persönliche Dateien werden dabei nicht verändert; eine Wiederherstellung betrifft " +
        "Systemdateien, Treiber und Programme, nicht die eigenen Dokumente.";

    public bool RequiresRestorePoint => false;   // legt selbst einen an
    public bool IsReversible => true;            // Systemschutz lässt sich wieder abschalten

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        var systemDrive = Path.GetPathRoot(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";

        progress.Report($"Aktiviere Systemschutz für {systemDrive} …");
        var enable = await ProcessRunner.RunAsync(
            "powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -Command " +
            $"\"try {{ Enable-ComputerRestore -Drive '{systemDrive}' -ErrorAction Stop; Write-Output 'OK' }} " +
            "catch { Write-Output ('FEHLER: ' + $_.Exception.Message) }\"",
            progress, ct).ConfigureAwait(false);

        if (enable.Output.Contains("FEHLER", StringComparison.OrdinalIgnoreCase))
            return new FixOutcome(false,
                "Der Systemschutz ließ sich nicht aktivieren. Auf manchen Systemen ist er per " +
                "Gruppenrichtlinie gesperrt. Manuell prüfen: Windows-Suche → " +
                "„Wiederherstellungspunkt erstellen“ → Laufwerk auswählen → „Konfigurieren“.");

        progress.Report("Lege ersten Wiederherstellungspunkt an …");
        var created = await _restorePoints
            .CreateAsync("DariaTech PC-Doktor: Ausgangszustand", ct)
            .ConfigureAwait(false);
        progress.Report(created.Message);

        // Erfolgskontrolle: Ist jetzt wirklich ein Punkt vorhanden?
        var points = DriveProtectionReader.ReadRestorePoints(ct);
        if (points.Count == 0)
            return new FixOutcome(false,
                "Der Systemschutz wurde aktiviert, es ist aber noch kein Wiederherstellungspunkt " +
                "vorhanden. Windows begrenzt die Häufigkeit neuer Punkte – bitte in einigen Minuten " +
                "erneut versuchen oder manuell einen Punkt anlegen.");

        var msg = $"Systemwiederherstellung aktiv – {points.Count} Wiederherstellungspunkt(e) vorhanden, " +
                  $"neuester vom {points[0]:dd.MM.yyyy HH:mm}.";
        progress.Report(msg);
        return new FixOutcome(true, msg);
    }
}
