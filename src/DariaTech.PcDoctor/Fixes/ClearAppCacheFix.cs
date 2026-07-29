using System.Diagnostics;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Leert die Zwischenspeicher (Caches) der gängigen Browser – Chrome, Edge,
/// Brave, Opera, Vivaldi und Firefox – über alle Profile hinweg.
///
/// Wichtig für die Verlässlichkeit: Läuft ein Browser, sind seine Cache-Dateien
/// gesperrt und können nicht gelöscht werden. Dieser Fix erkennt das, prüft nach
/// dem Aufräumen die verbleibende Cache-Größe und meldet ehrlich, was übrig
/// blieb und warum – statt fälschlich Erfolg zu melden.
/// </summary>
public sealed class ClearAppCacheFix : IFixAction
{
    /// <summary>Unterhalb dieser Restgröße gilt der Cache als geleert (Rest sind Steuerdateien).</summary>
    private const long AcceptableRemainderBytes = 10L * 1024 * 1024;   // 10 MB

    public string Title => "App-Caches leeren (Browser)";

    public string Description =>
        "Leert die Zwischenspeicher (Caches) von Google Chrome, Microsoft Edge, Brave, Opera, Vivaldi " +
        "und Mozilla Firefox – über alle Profile hinweg. Lesezeichen, Passwörter, offene Tabs und " +
        "Verlauf bleiben erhalten; nur zwischengespeicherte Webinhalte werden entfernt (Webseiten laden " +
        "beim ersten Besuch danach etwas langsamer).\n\n" +
        "WICHTIG: Laufende Browser sperren ihre Cache-Dateien. Bitte alle Browser vorher schließen – " +
        "sonst kann der Cache nicht vollständig geleert werden. Die App prüft anschließend nach und " +
        "meldet, was nicht entfernt werden konnte.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;

    public Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var targets = BrowserCacheCatalog.Build(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

            if (targets.Count == 0)
            {
                const string none = "Keine Browser-Caches gefunden – nichts zu leeren.";
                progress.Report(none);
                return new FixOutcome(true, none);
            }

            var total = CleanupResult.Empty;
            long remainingTotal = 0;
            var blocked = new List<string>();      // Browser, die noch laufen und Reste haben
            var perBrowser = new List<string>();

            foreach (var target in targets)
            {
                ct.ThrowIfCancellationRequested();

                var running = RunningProcesses(target);
                if (running)
                    progress.Report($"⚠ {target.Name} läuft – gesperrte Dateien können nicht entfernt werden.");

                progress.Report($"Leere Cache: {target.Name} ({target.CacheDirectories.Count} Ordner) …");

                var result = CleanupResult.Empty;
                foreach (var dir in target.CacheDirectories)
                {
                    ct.ThrowIfCancellationRequested();
                    result = result.Add(DirectoryCleaner.ClearContents(dir, ct));
                }

                // Nachprüfen: Was ist tatsächlich noch da?
                long remaining = 0;
                foreach (var dir in target.CacheDirectories)
                {
                    ct.ThrowIfCancellationRequested();
                    remaining += DirectoryCleaner.Size(dir, ct);
                }

                total = total.Add(result);
                remainingTotal += remaining;

                var line = $"{target.Name}: {ByteSize.Human(result.FreedBytes)} entfernt";
                if (remaining > AcceptableRemainderBytes)
                {
                    line += $", {ByteSize.Human(remaining)} noch vorhanden";
                    if (running)
                    {
                        line += " (Browser läuft)";
                        blocked.Add(target.Name);
                    }
                }
                perBrowser.Add(line);
                progress.Report("  " + line);
            }

            return BuildOutcome(progress, total, remainingTotal, blocked, perBrowser);
        }, ct);

    /// <summary>Baut ein ehrliches Ergebnis: Erfolg nur, wenn wirklich kaum Cache übrig ist.</summary>
    private static FixOutcome BuildOutcome(
        IProgress<string> progress,
        CleanupResult total,
        long remainingTotal,
        List<string> blocked,
        List<string> perBrowser)
    {
        var summary = $"{ByteSize.Human(total.FreedBytes)} freigegeben ({total.Deleted} Objekte). " +
                      string.Join(" · ", perBrowser);

        // Nennenswerte Reste + laufende Browser -> das ist KEIN Erfolg.
        if (remainingTotal > AcceptableRemainderBytes && blocked.Count > 0)
        {
            var msg = summary +
                      $"\n\nNicht vollständig geleert: {ByteSize.Human(remainingTotal)} Cache sind noch " +
                      $"vorhanden, weil {string.Join(" und ", blocked)} gerade läuft/laufen. " +
                      "Bitte diese Browser schließen und die Reparatur erneut ausführen.";
            progress.Report(msg);
            return new FixOutcome(false, msg);
        }

        if (remainingTotal > AcceptableRemainderBytes)
        {
            var msg = summary +
                      $"\n\nHinweis: {ByteSize.Human(remainingTotal)} konnten nicht entfernt werden " +
                      "(Dateien in Benutzung oder kein Zugriff). Nach einem Neustart erneut versuchen.";
            progress.Report(msg);
            return new FixOutcome(false, msg);
        }

        var ok = summary + "\n\nCaches vollständig geleert.";
        progress.Report(ok);
        return new FixOutcome(true, ok);
    }

    /// <summary>True, wenn mindestens ein Prozess des Browsers läuft.</summary>
    private static bool RunningProcesses(BrowserCacheTarget target)
    {
        foreach (var name in target.ProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var p in processes) p.Dispose();
                if (processes.Length > 0) return true;
            }
            catch { /* Prozessliste nicht lesbar – als „läuft nicht" behandeln */ }
        }
        return false;
    }
}
