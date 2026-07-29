using System.Diagnostics;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Startet die Windows-Texteingabe neu (<c>ctfmon.exe</c> und
/// <c>TextInputHost.exe</c>). Behebt das häufige Symptom „in der Windows-Suche
/// lässt sich nichts tippen" – dann hängt meist die Texteingabe-Komponente.
/// Unkritisch und sofort wirksam: die Prozesse werden von Windows automatisch
/// wieder gestartet, es gehen keine Daten verloren.
/// </summary>
public sealed class TextInputRestartFix : IFixAction
{
    public string Title => "Texteingabe neu starten";

    public string Description =>
        "Startet die Windows-Texteingabe neu. Hilft, wenn sich in der Windows-Suche (oder in " +
        "Eingabefeldern des Startmenüs) nichts mehr tippen lässt – dann hängt meist der Dienst " +
        "für die Texteingabe. Die beteiligten Prozesse werden beendet und sofort neu gestartet; " +
        "es gehen keine Daten verloren und es wird nichts am System verändert. " +
        "Offene Programme sind nicht betroffen.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;   // reiner Prozess-Neustart, keine dauerhafte Änderung

    private static readonly string[] Processes = { "ctfmon", "TextInputHost" };

    public Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var stopped = 0;

            foreach (var name in Processes)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    foreach (var proc in Process.GetProcessesByName(name))
                    {
                        using (proc)
                        {
                            progress.Report($"Beende {name} …");
                            try
                            {
                                proc.Kill();
                                proc.WaitForExit(5000);
                                stopped++;
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

            // ctfmon wieder starten (TextInputHost startet Windows bei Bedarf selbst).
            var restarted = false;
            try
            {
                progress.Report("Starte Texteingabe (ctfmon) …");
                Process.Start(new ProcessStartInfo("ctfmon.exe") { UseShellExecute = true });
                restarted = true;
            }
            catch (Exception ex)
            {
                progress.Report($"ctfmon konnte nicht gestartet werden: {ex.Message}");
            }

            var msg = restarted
                ? $"Texteingabe neu gestartet ({stopped} Prozess(e) erneuert). " +
                  "Bitte die Windows-Suche erneut testen – ist weiterhin keine Eingabe möglich, " +
                  "zusätzlich „Systemdateien reparieren (SFC + DISM)“ ausführen."
                : $"{stopped} Prozess(e) beendet, ctfmon konnte aber nicht gestartet werden. " +
                  "Nach einem Neustart des PCs läuft die Texteingabe wieder.";

            progress.Report(msg);
            return new FixOutcome(restarted, msg);
        }, ct);
}
