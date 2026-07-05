using System.IO;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Findet die größten „Speicherfresser" auf dem Systemlaufwerk: temporäre
/// Dateien, Windows-Update-Cache, Papierkorb, Windows.old, Ruhezustandsdatei und
/// den Downloads-Ordner. Zeigt je Fundort die belegte Größe und schätzt, wie viel
/// sich gefahrlos freigeben lässt. Rein lesend – es wird nichts gelöscht (dafür
/// gibt es die Aufräum-Reparaturen).
/// </summary>
public sealed class DiskUsageCheck : ICheck
{
    public string Area => "Speicherplatz-Fresser";

    // Ordner werden nur ab dieser Größe einzeln gelistet (weniger Rauschen).
    private const long ListThreshold = 100L * 1024 * 1024;   // 100 MB

    // Nur systemeigene, gefahrlos leerbare Fundorte zählen als „freigebbar".
    private const long ReclaimHintThreshold = 2L * 1024 * 1024 * 1024; // 2 GB

    private static readonly EnumerationOptions WalkOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint   // Junctions/Symlinks nicht folgen
    };

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var systemDrive = Path.GetPathRoot(windows) ?? @"C:\";
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            long reclaimable = 0;

            // --- gefahrlos leerbar ---
            var temp = DirSize(Path.GetTempPath(), ct)
                       + DirSize(Path.Combine(windows, "Temp"), ct);
            reclaimable += temp;
            AddFolder(results, "Temporäre Dateien", temp,
                "Zwischendateien von Windows und Programmen. Gefahrlos leerbar über die Reparatur " +
                "„Temporäre Dateien leeren“.");

            var updateCache = DirSize(Path.Combine(windows, "SoftwareDistribution", "Download"), ct);
            reclaimable += updateCache;
            AddFolder(results, "Windows-Update-Cache", updateCache,
                "Bereits installierte Update-Downloads. Können gefahrlos entfernt werden; Windows lädt " +
                "bei Bedarf neu.");

            var recycle = RecycleBinSize(ct);
            reclaimable += recycle;
            AddFolder(results, "Papierkorb", recycle,
                "Gelöschte Dateien aller Laufwerke. Über „Papierkorb leeren“ endgültig entfernen.");

            var windowsOld = DirSize(Path.Combine(systemDrive, "Windows.old"), ct);
            if (windowsOld > 0)
            {
                reclaimable += windowsOld;
                results.Add(new CheckResult(Area, "Windows.old", ByteSize.Human(windowsOld), Severity.Warning,
                    Detail: "Reste einer früheren Windows-Version. Nach einem stabilen Upgrade nicht mehr nötig " +
                            "und oft mehrere Gigabyte groß.",
                    Tip: "So entfernen: Einstellungen → „System“ → „Speicher“ → „Temporäre Dateien“ → " +
                         "„Vorherige Windows-Installation(en)“ auswählen und entfernen.",
                    OpenTarget: "ms-settings:storagesense"));
            }

            // --- Hinweis, nicht per Löschen freigebbar ---
            var hiberfil = FileSize(Path.Combine(systemDrive, "hiberfil.sys"));
            if (hiberfil > ListThreshold)
                results.Add(new CheckResult(Area, "Ruhezustandsdatei (hiberfil.sys)", ByteSize.Human(hiberfil),
                    Severity.Info,
                    Detail: "Reserviert Speicher für den Ruhezustand. Nur deaktivieren, wenn der Ruhezustand " +
                            "nicht gebraucht wird.",
                    Tip: "So verkleinern/entfernen: Eingabeaufforderung als Administrator → „powercfg /h off“ " +
                         "(schaltet den Ruhezustand aus und gibt die Datei frei)."));

            var downloads = DirSize(Path.Combine(profile, "Downloads"), ct);
            if (downloads > ListThreshold)
                results.Add(new CheckResult(Area, "Downloads-Ordner", ByteSize.Human(downloads), Severity.Info,
                    Detail: "Enthält heruntergeladene Dateien – darunter oft alte Installer, die man nicht mehr " +
                            "braucht. Enthält aber auch eigene Dateien, daher vor dem Löschen durchsehen.",
                    Tip: "Ordner öffnen und nach Größe sortieren; alte Setup-Dateien (.exe/.msi/.zip) löschen."));

            // --- Gesamtbewertung an den Anfang ---
            var overall = reclaimable >= ReclaimHintThreshold
                ? new CheckResult(Area, "Gefahrlos freigebbar", ByteSize.Human(reclaimable), Severity.Warning,
                    Detail: $"Rund {ByteSize.Human(reclaimable)} in temporären Dateien, Update-Cache, Papierkorb " +
                            "und Windows.old lassen sich ohne Datenverlust freigeben.",
                    Tip: "Über die Reparaturen „Temporäre Dateien leeren“ und „App-Caches leeren“ sowie die " +
                         "Windows-Datenträgerbereinigung freigeben.")
                : new CheckResult(Area, "Gefahrlos freigebbar", ByteSize.Human(reclaimable), Severity.Ok,
                    Detail: "Kein nennenswerter Ballast aus temporären Dateien/Caches gefunden.");
            results.Insert(0, overall);

            return results;
        }, ct);

    private void AddFolder(List<CheckResult> results, string label, long size, string detail)
    {
        if (size < ListThreshold) return;
        results.Add(new CheckResult(Area, label, ByteSize.Human(size), Severity.Info, Detail: detail));
    }

    /// <summary>Summiert den Papierkorb (<c>$Recycle.Bin</c>) über alle festen Laufwerke.</summary>
    private static long RecycleBinSize(CancellationToken ct)
    {
        long total = 0;
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch { return 0; }

        foreach (var drive in drives)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                total += DirSize(Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin"), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* Laufwerk nicht lesbar */ }
        }
        return total;
    }

    private static long DirSize(string path, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", WalkOptions))
            {
                ct.ThrowIfCancellationRequested();
                try { total += new FileInfo(file).Length; }
                catch { /* Datei verschwunden/gesperrt */ }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Ordner nicht (mehr) lesbar */ }
        return total;
    }

    private static long FileSize(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch { return 0; }
    }
}
