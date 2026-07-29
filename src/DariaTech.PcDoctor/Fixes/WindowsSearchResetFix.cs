using System.IO;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Setzt die Windows-Suche zurück: Suchdienst stoppen, den beschädigten
/// Suchindex (<c>Windows.edb</c>) entfernen, Dienst wieder starten. Windows baut
/// den Index anschließend selbstständig neu auf.
///
/// Typischer Anwendungsfall: Die Suche nimmt keine Eingaben mehr an oder der
/// Explorer schließt sich beim Suchen (häufig nach fehlerhaften Updates).
/// Es werden keine Nutzerdaten angefasst – nur der Suchindex, der reine
/// Zwischenspeicherung ist.
/// </summary>
public sealed class WindowsSearchResetFix : IFixAction
{
    public string Title => "Windows-Suche zurücksetzen";

    public string Description =>
        "Behebt Probleme mit der Windows-Suche (keine Eingabe möglich, Explorer schließt sich beim Suchen, " +
        "Suche findet nichts). Dazu wird der Suchdienst gestoppt, der beschädigte Suchindex gelöscht und " +
        "der Dienst neu gestartet – Windows baut den Index danach automatisch neu auf. " +
        "Es werden KEINE Dateien, Dokumente oder Einstellungen gelöscht, nur der Index (ein Zwischenspeicher). " +
        "Hinweis: Bis der Index vollständig neu aufgebaut ist, kann die Suche einige Zeit unvollständige " +
        "Ergebnisse liefern (je nach Datenmenge einige Minuten bis wenige Stunden).";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;   // Index wird neu aufgebaut, nicht wiederhergestellt

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Stoppe Windows-Suchdienst (WSearch) …");
        await ProcessRunner.RunAsync("net.exe", "stop wsearch", progress, ct).ConfigureAwait(false);

        var indexPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Microsoft", "Search", "Data", "Applications", "Windows", "Windows.edb");

        var removed = false;
        string? removeError = null;
        try
        {
            if (File.Exists(indexPath))
            {
                File.SetAttributes(indexPath, FileAttributes.Normal);
                File.Delete(indexPath);
                removed = true;
                progress.Report("Beschädigter Suchindex entfernt.");
            }
            else
            {
                progress.Report("Kein Suchindex vorhanden – wird neu aufgebaut.");
            }
        }
        catch (Exception ex)
        {
            removeError = ex.Message;
            progress.Report($"Suchindex konnte nicht entfernt werden: {ex.Message}");
        }

        progress.Report("Starte Windows-Suchdienst …");
        var start = await ProcessRunner.RunAsync("net.exe", "start wsearch", progress, ct)
            .ConfigureAwait(false);

        if (start.ExitCode != 0)
            return new FixOutcome(false,
                "Der Suchdienst konnte nicht gestartet werden. Bitte den PC neu starten – " +
                "der Dienst startet dann automatisch und baut den Index neu auf.");

        if (removeError is not null)
            return new FixOutcome(false,
                "Der Suchindex ließ sich nicht löschen (Datei in Benutzung). Bitte den PC neu starten " +
                "und die Reparatur erneut ausführen.");

        return new FixOutcome(true,
            removed
                ? "Windows-Suche zurückgesetzt. Der Suchindex wird jetzt im Hintergrund neu aufgebaut – " +
                  "bis dahin kann die Suche unvollständige Ergebnisse liefern."
                : "Suchdienst neu gestartet. Der Suchindex wird im Hintergrund neu aufgebaut.");
    }
}
