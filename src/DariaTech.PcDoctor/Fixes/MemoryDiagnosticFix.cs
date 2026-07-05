using System.Diagnostics;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Öffnet die Windows-Speicherdiagnose (<c>MdSched.exe</c>). Der eigentliche
/// RAM-Test läuft aus technischen Gründen erst nach einem Neustart – der Nutzer
/// wählt im Windows-Dialog, ob sofort neu gestartet oder beim nächsten Start
/// geprüft werden soll. Rein diagnostisch, keine Systemänderung (umkehrbar).
/// </summary>
public sealed class MemoryDiagnosticFix : IFixAction
{
    public string Title => "Arbeitsspeicher testen (Windows-Speicherdiagnose)";

    public string Description =>
        "Öffnet die in Windows integrierte Speicherdiagnose zum Prüfen des Arbeitsspeichers (RAM) " +
        "auf Fehler – hilfreich bei Abstürzen/Bluescreens ohne klare Ursache. Der Test selbst läuft " +
        "erst nach einem Neustart (Dauer je nach RAM-Größe ca. 15–30 Minuten); im folgenden Dialog " +
        "kann gewählt werden, ob sofort neu gestartet oder erst beim nächsten Start geprüft wird. " +
        "Das Ergebnis erscheint nach dem Neustart als Windows-Meldung und im Ereignisprotokoll. " +
        "Es wird nichts verändert – ein reiner Test.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;

    public Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        try
        {
            progress.Report("Öffne Windows-Speicherdiagnose …");
            // GUI-Werkzeug: mit ShellExecute starten (kein Ausgabe-Streaming/kein Warten).
            Process.Start(new ProcessStartInfo("MdSched.exe") { UseShellExecute = true });

            var msg = "Windows-Speicherdiagnose geöffnet. Bitte im Dialog wählen, wann der RAM-Test " +
                      "ausgeführt werden soll (sofortiger Neustart oder beim nächsten Start).";
            progress.Report(msg);
            return Task.FromResult(new FixOutcome(true, msg));
        }
        catch (Exception ex)
        {
            var msg = $"Speicherdiagnose konnte nicht geöffnet werden: {ex.Message}";
            progress.Report(msg);
            return Task.FromResult(new FixOutcome(false, msg));
        }
    }
}
