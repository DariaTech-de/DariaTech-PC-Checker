using System.Diagnostics;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Startet die Oberflächen-Prozesse von Startmenü, Taskleiste und
/// Suchoberfläche neu (<c>StartMenuExperienceHost</c>, <c>ShellExperienceHost</c>,
/// <c>SearchHost</c>). Behebt das häufige „Startmenü/Taskleiste reagiert nicht“.
///
/// Windows startet diese Prozesse automatisch neu – es gehen keine Daten
/// verloren, offene Programme bleiben geöffnet.
/// </summary>
public sealed class RestartStartMenuFix : IFixAction
{
    /// <summary>Prozesse der Windows-Oberfläche, die sich selbst neu starten.</summary>
    private static readonly string[] ShellProcesses =
    {
        "StartMenuExperienceHost",
        "ShellExperienceHost",
        "SearchHost",
        "SearchApp",
    };

    public string Title => "Startmenü & Taskleiste neu starten";

    public string Description =>
        "Startet die Oberflächen-Prozesse von Startmenü, Taskleiste und Suche neu. Hilft, wenn das " +
        "Startmenü sich nicht öffnet, die Taskleiste nicht reagiert oder Klicks ins Leere gehen. " +
        "Windows startet diese Prozesse sofort selbst wieder – offene Programme und ungespeicherte " +
        "Dokumente sind NICHT betroffen. Der Bildschirm kann dabei kurz flackern.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;   // reiner Prozess-Neustart

    public Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var restarted = 0;
            var notRunning = 0;

            foreach (var name in ShellProcesses)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var processes = Process.GetProcessesByName(name);
                    if (processes.Length == 0)
                    {
                        notRunning++;
                        continue;
                    }

                    foreach (var process in processes)
                    {
                        using (process)
                        {
                            progress.Report($"Beende {name} …");
                            try
                            {
                                process.Kill();
                                process.WaitForExit(5000);
                                restarted++;
                            }
                            catch (Exception ex)
                            {
                                progress.Report($"{name} konnte nicht beendet werden: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    progress.Report($"{name} nicht prüfbar: {ex.Message}");
                }
            }

            if (restarted == 0)
            {
                var none = notRunning == ShellProcesses.Length
                    ? "Die Oberflächen-Prozesse liefen nicht – vermutlich ist die Windows-Oberfläche bereits " +
                      "neu gestartet. Reagiert die Taskleiste weiterhin nicht, zusätzlich „Explorer neu starten“ " +
                      "und danach „Systemdateien reparieren“ ausführen."
                    : "Es konnte kein Oberflächen-Prozess neu gestartet werden.";
                progress.Report(none);
                return new FixOutcome(restarted > 0, none);
            }

            var msg = $"{restarted} Oberflächen-Prozess(e) neu gestartet. Startmenü und Taskleiste sollten " +
                      "nach wenigen Sekunden wieder reagieren. Falls nicht: „Explorer neu starten“, danach " +
                      "„Systemdateien reparieren (SFC + DISM)“.";
            progress.Report(msg);
            return new FixOutcome(true, msg);
        }, ct);
}
