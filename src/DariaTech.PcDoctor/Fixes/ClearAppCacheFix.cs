using System.IO;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Leert die Caches gängiger Browser (Chrome, Edge, Firefox). Aktuell genutzte
/// Dateien (Browser offen) werden übersprungen. Unkritisch – kein
/// Wiederherstellungspunkt nötig.
/// </summary>
public sealed class ClearAppCacheFix : IFixAction
{
    public string Title => "App-Caches leeren (Browser)";
    public string Description =>
        "Leert die Zwischenspeicher (Caches) von Google Chrome, Microsoft Edge " +
        "und Mozilla Firefox. Geöffnete Browser bitte vorher schließen – gesperrte " +
        "Dateien werden sonst übersprungen.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;

    public Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
        => Task.Run(() =>
        {
            long freed = 0;
            int deleted = 0, skipped = 0;

            foreach (var folder in CacheFolders())
            {
                if (!Directory.Exists(folder)) continue;
                progress.Report($"Leere Cache: {folder}");

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
                    catch { skipped++; }
                }
            }

            var msg = $"{deleted} Cache-Objekt(e) gelöscht, {freed / 1024d / 1024d:N1} MB freigegeben" +
                      (skipped > 0 ? $", {skipped} übersprungen (Browser geöffnet?)." : ".");
            progress.Report(msg);
            return new FixOutcome(true, msg);
        }, ct);

    private static IEnumerable<string> CacheFolders()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Chromium-basiert (Chrome/Edge): je Profil mehrere Cache-Ordner.
        foreach (var userData in new[]
                 {
                     Path.Combine(local, "Google", "Chrome", "User Data"),
                     Path.Combine(local, "Microsoft", "Edge", "User Data"),
                     Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")
                 })
        {
            if (!Directory.Exists(userData)) continue;
            foreach (var profile in SafeDirs(userData))
            {
                yield return Path.Combine(profile, "Cache");
                yield return Path.Combine(profile, "Code Cache");
                yield return Path.Combine(profile, "GPUCache");
                yield return Path.Combine(profile, "Service Worker", "CacheStorage");
            }
        }

        // Firefox: je Profil ein cache2-Ordner.
        var firefox = Path.Combine(local, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefox))
            foreach (var profile in SafeDirs(firefox))
                yield return Path.Combine(profile, "cache2");

        // Firefox legt Profile teils auch unter Roaming an.
        var firefoxRoaming = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxRoaming))
            foreach (var profile in SafeDirs(firefoxRoaming))
                yield return Path.Combine(profile, "cache2");
    }

    private static IEnumerable<string> SafeDirs(string path)
    {
        try { return Directory.EnumerateDirectories(path); }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> EnumerateSafe(string folder)
    {
        try { return Directory.EnumerateFileSystemEntries(folder); }
        catch { return Array.Empty<string>(); }
    }
}
