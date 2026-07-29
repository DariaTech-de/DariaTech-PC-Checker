using System.IO;

namespace DariaTech.PcDoctor.Core;

/// <summary>Ein Browser mit seinen Prozessnamen und Cache-Verzeichnissen.</summary>
/// <param name="Name">Anzeigename, z. B. „Google Chrome".</param>
/// <param name="ProcessNames">Prozessnamen ohne Endung (für die Erkennung „läuft gerade").</param>
/// <param name="CacheDirectories">Vollständige Pfade der zu leerenden Cache-Ordner.</param>
public sealed record BrowserCacheTarget(
    string Name,
    IReadOnlyList<string> ProcessNames,
    IReadOnlyList<string> CacheDirectories);

/// <summary>
/// Baut die Liste der Browser-Cache-Verzeichnisse auf. Die Basisordner werden
/// übergeben, damit die Pfadlogik ohne echtes Benutzerprofil testbar ist.
///
/// Wichtig: Chromium-Browser legen Caches an mehreren Stellen ab – je Profil
/// (Cache, Code Cache, GPUCache, Media Cache, Service Worker …) UND auf Ebene
/// von „User Data" (ShaderCache, GrShaderCache). Fehlt einer davon, bleibt nach
/// dem Aufräumen sichtbar Cache übrig.
/// </summary>
public static class BrowserCacheCatalog
{
    /// <summary>Cache-Ordner innerhalb eines Chromium-Profils.</summary>
    private static readonly string[] ProfileCacheFolders =
    {
        "Cache",
        "Code Cache",
        "GPUCache",
        "DawnCache",
        "DawnGraphiteCache",
        "DawnWebGPUCache",
        "Media Cache",
        "GrShaderCache",
        "ShaderCache",
        // Bewusst NICHT dabei: „Storage" / „Local Storage" / „IndexedDB" – das sind
        // Nutzer-/Erweiterungsdaten (Anmeldungen, Add-on-Einstellungen), kein Cache.
    };

    /// <summary>Cache-Ordner auf Ebene von „User Data" (profilübergreifend).</summary>
    private static readonly string[] RootCacheFolders =
    {
        "ShaderCache",
        "GrShaderCache",
        "GraphiteDawnCache",
    };

    /// <summary>Ordner, die KEINE Profile sind und daher übersprungen werden.</summary>
    private static readonly HashSet<string> NonProfileFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Crashpad", "ShaderCache", "GrShaderCache", "GraphiteDawnCache",
        "SwReporter", "Safe Browsing", "Subresource Filter", "WidevineCdm",
        "component_crx_cache", "extensions_crx_cache", "Snapshots",
        "BrowserMetrics", "Local Traces", "CertificateRevocation",
        "OnDeviceHeadSuggestModel", "OptimizationHints", "PKIMetadata",
        "SSLErrorAssistant", "TrustTokenKeyCommitments", "MEIPreload",
        "FileTypePolicies", "OriginTrials", "ZxcvbnData", "AutofillStates",
        "Webstore Downloads", "hyphen-data", "segmentation_platform",
    };

    /// <summary>
    /// Ermittelt alle Browser-Cache-Ziele. Es werden nur real existierende
    /// Verzeichnisse zurückgegeben.
    /// </summary>
    public static IReadOnlyList<BrowserCacheTarget> Build(string localAppData, string roamingAppData)
    {
        var targets = new List<BrowserCacheTarget>();

        // --- Chromium-basierte Browser ---
        AddChromium(targets, "Google Chrome", new[] { "chrome" },
            Path.Combine(localAppData, "Google", "Chrome", "User Data"));
        AddChromium(targets, "Microsoft Edge", new[] { "msedge" },
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data"));
        AddChromium(targets, "Brave", new[] { "brave" },
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"));
        AddChromium(targets, "Opera", new[] { "opera" },
            Path.Combine(roamingAppData, "Opera Software", "Opera Stable"));
        AddChromium(targets, "Vivaldi", new[] { "vivaldi" },
            Path.Combine(localAppData, "Vivaldi", "User Data"));

        // --- Firefox (eigene Struktur: cache2 je Profil, unter Local und Roaming) ---
        var firefoxDirs = new List<string>();
        foreach (var root in new[]
                 {
                     Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles"),
                     Path.Combine(roamingAppData, "Mozilla", "Firefox", "Profiles")
                 })
        {
            foreach (var profile in SafeDirs(root))
            {
                AddIfExists(firefoxDirs, Path.Combine(profile, "cache2"));
                AddIfExists(firefoxDirs, Path.Combine(profile, "startupCache"));
                AddIfExists(firefoxDirs, Path.Combine(profile, "shader-cache"));
            }
        }
        if (firefoxDirs.Count > 0)
            targets.Add(new BrowserCacheTarget("Mozilla Firefox", new[] { "firefox" }, firefoxDirs));

        return targets;
    }

    private static void AddChromium(
        List<BrowserCacheTarget> targets, string name, string[] processNames, string userDataRoot)
    {
        if (!Directory.Exists(userDataRoot)) return;

        var dirs = new List<string>();

        // Profilübergreifende Caches direkt unter „User Data".
        foreach (var folder in RootCacheFolders)
            AddIfExists(dirs, Path.Combine(userDataRoot, folder));

        // Caches je Profil (Default, Profile 1, …).
        foreach (var profile in SafeDirs(userDataRoot))
        {
            var profileName = Path.GetFileName(profile);
            if (NonProfileFolders.Contains(profileName)) continue;

            foreach (var folder in ProfileCacheFolders)
                AddIfExists(dirs, Path.Combine(profile, folder));

            // Service-Worker-Caches liegen eine Ebene tiefer.
            AddIfExists(dirs, Path.Combine(profile, "Service Worker", "CacheStorage"));
            AddIfExists(dirs, Path.Combine(profile, "Service Worker", "ScriptCache"));
        }

        if (dirs.Count > 0)
            targets.Add(new BrowserCacheTarget(name, processNames, dirs));
    }

    private static void AddIfExists(List<string> list, string path)
    {
        if (Directory.Exists(path)) list.Add(path);
    }

    private static IEnumerable<string> SafeDirs(string path)
    {
        try { return Directory.Exists(path) ? Directory.EnumerateDirectories(path) : Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }
}
