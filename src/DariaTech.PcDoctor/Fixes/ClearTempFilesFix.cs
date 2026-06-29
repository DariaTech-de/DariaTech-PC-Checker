using System.IO;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Leert die temporären Ordner (%temp% und Windows\Temp). Unkritisch – kein
/// Wiederherstellungspunkt nötig; in Benutzung befindliche Dateien werden
/// übersprungen.
/// </summary>
public sealed class ClearTempFilesFix : IFixAction
{
    public string Title => "Temporäre Dateien leeren";
    public string Description =>
        "Löscht den Inhalt der Temp-Ordner (%temp% und Windows\\Temp). " +
        "Aktuell genutzte Dateien werden übersprungen.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;

    public Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
        => Task.Run(() =>
        {
            long freed = 0;
            int deleted = 0, skipped = 0;

            foreach (var folder in TempFolders())
            {
                if (!Directory.Exists(folder)) continue;
                progress.Report($"Räume auf: {folder}");

                foreach (var path in EnumerateSafe(folder))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.Exists(path))
                        {
                            var size = new FileInfo(path).Length;
                            File.SetAttributes(path, FileAttributes.Normal);
                            File.Delete(path);
                            freed += size;
                            deleted++;
                        }
                        else if (Directory.Exists(path))
                        {
                            Directory.Delete(path, recursive: true);
                            deleted++;
                        }
                    }
                    catch { skipped++; } // in Benutzung / kein Zugriff
                }
            }

            var freedMb = freed / 1024d / 1024d;
            var msg = $"{deleted} Objekt(e) gelöscht, {freedMb:N1} MB freigegeben" +
                      (skipped > 0 ? $", {skipped} übersprungen (in Benutzung)." : ".");
            progress.Report(msg);
            return new FixOutcome(true, msg);
        }, ct);

    private static IEnumerable<string> TempFolders()
    {
        yield return Path.GetTempPath();
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windows))
            yield return Path.Combine(windows, "Temp");
    }

    private static IEnumerable<string> EnumerateSafe(string folder)
    {
        try { return Directory.EnumerateFileSystemEntries(folder); }
        catch { return Array.Empty<string>(); }
    }
}
