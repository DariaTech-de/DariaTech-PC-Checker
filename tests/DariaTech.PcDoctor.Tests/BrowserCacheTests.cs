using System.IO;
using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

/// <summary>
/// Deckt den gemeldeten Fehler ab: „Chrome-Cache wurde nicht gelöscht, er ist
/// noch vorhanden." Ursachen waren eine unvollständige Ordnerliste und eine
/// fehlende Erfolgskontrolle. Die Tests arbeiten mit echten temporären
/// Verzeichnissen und laufen daher auf jeder Plattform.
/// </summary>
public sealed class BrowserCacheTests : IDisposable
{
    private readonly string _root;
    private readonly string _local;
    private readonly string _roaming;

    public BrowserCacheTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pcdoktor-tests-" + Guid.NewGuid().ToString("N"));
        _local = Path.Combine(_root, "Local");
        _roaming = Path.Combine(_root, "Roaming");
        Directory.CreateDirectory(_local);
        Directory.CreateDirectory(_roaming);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* Aufräumen ist best effort */ }
    }

    private string CreateDir(params string[] parts)
    {
        var path = Path.Combine(parts);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string directory, string name, int bytes)
        => File.WriteAllBytes(Path.Combine(directory, name), new byte[bytes]);

    // ---------- DirectoryCleaner ----------

    [Fact]
    public void ClearContents_RemovesFilesAndSubfolders_KeepsFolderItself()
    {
        var cache = CreateDir(_local, "cache");
        WriteFile(cache, "a.bin", 1000);
        var sub = CreateDir(cache, "sub");
        WriteFile(sub, "b.bin", 2000);

        var result = DirectoryCleaner.ClearContents(cache);

        Assert.True(Directory.Exists(cache));                 // Ordner bleibt
        Assert.Empty(Directory.EnumerateFileSystemEntries(cache));
        Assert.Equal(3000, result.FreedBytes);                // Datei + Unterordnerinhalt
        Assert.Equal(2, result.Deleted);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void ClearContents_MissingDirectory_IsNoOp()
    {
        var result = DirectoryCleaner.ClearContents(Path.Combine(_local, "gibtesnicht"));
        Assert.Equal(CleanupResult.Empty, result);
    }

    [Fact]
    public void Size_SumsRecursively_AndIsZeroAfterClearing()
    {
        var cache = CreateDir(_local, "size");
        WriteFile(cache, "a.bin", 500);
        WriteFile(CreateDir(cache, "deep", "deeper"), "b.bin", 1500);

        Assert.Equal(2000, DirectoryCleaner.Size(cache));

        DirectoryCleaner.ClearContents(cache);
        Assert.Equal(0, DirectoryCleaner.Size(cache));   // Erfolgskontrolle, die vorher fehlte
    }

    [Fact]
    public void CleanupResult_Add_AccumulatesAllCounters()
    {
        var sum = new CleanupResult(100, 1, 2).Add(new CleanupResult(50, 3, 4));
        Assert.Equal(150, sum.FreedBytes);
        Assert.Equal(4, sum.Deleted);
        Assert.Equal(6, sum.Skipped);
    }

    // ---------- BrowserCacheCatalog ----------

    [Fact]
    public void Build_FindsChromeProfileCaches_IncludingNewerFolders()
    {
        var userData = Path.Combine(_local, "Google", "Chrome", "User Data");
        CreateDir(userData, "Default", "Cache");
        CreateDir(userData, "Default", "Code Cache");
        CreateDir(userData, "Default", "GPUCache");
        CreateDir(userData, "Default", "DawnCache");        // war vorher nicht abgedeckt
        CreateDir(userData, "Default", "Media Cache");      // war vorher nicht abgedeckt
        CreateDir(userData, "ShaderCache");                 // profiluebergreifend, war nicht abgedeckt

        var chrome = Assert.Single(BrowserCacheCatalog.Build(_local, _roaming));

        Assert.Equal("Google Chrome", chrome.Name);
        Assert.Contains("chrome", chrome.ProcessNames);
        Assert.Contains(chrome.CacheDirectories, d => d.EndsWith("DawnCache", StringComparison.Ordinal));
        Assert.Contains(chrome.CacheDirectories, d => d.EndsWith("Media Cache", StringComparison.Ordinal));
        Assert.Contains(chrome.CacheDirectories, d => d.EndsWith("ShaderCache", StringComparison.Ordinal));
        Assert.Equal(6, chrome.CacheDirectories.Count);
    }

    [Fact]
    public void Build_CoversAllProfiles_NotOnlyDefault()
    {
        var userData = Path.Combine(_local, "Google", "Chrome", "User Data");
        CreateDir(userData, "Default", "Cache");
        CreateDir(userData, "Profile 1", "Cache");
        CreateDir(userData, "Profile 2", "Cache");

        var chrome = Assert.Single(BrowserCacheCatalog.Build(_local, _roaming));
        Assert.Equal(3, chrome.CacheDirectories.Count);
    }

    [Fact]
    public void Build_SkipsNonProfileFolders()
    {
        var userData = Path.Combine(_local, "Google", "Chrome", "User Data");
        CreateDir(userData, "Default", "Cache");
        CreateDir(userData, "Crashpad", "Cache");     // kein Profil -> ignorieren
        CreateDir(userData, "SwReporter", "Cache");   // kein Profil -> ignorieren

        var chrome = Assert.Single(BrowserCacheCatalog.Build(_local, _roaming));
        Assert.Single(chrome.CacheDirectories);
        Assert.Contains("Default", chrome.CacheDirectories[0]);
    }

    [Fact]
    public void Build_FindsFirefoxProfiles_UnderLocalAndRoaming()
    {
        CreateDir(_local, "Mozilla", "Firefox", "Profiles", "abc.default", "cache2");
        CreateDir(_roaming, "Mozilla", "Firefox", "Profiles", "xyz.dev", "startupCache");

        var firefox = Assert.Single(BrowserCacheCatalog.Build(_local, _roaming));
        Assert.Equal("Mozilla Firefox", firefox.Name);
        Assert.Equal(2, firefox.CacheDirectories.Count);
    }

    [Fact]
    public void Build_SeparatesMultipleBrowsers()
    {
        CreateDir(_local, "Google", "Chrome", "User Data", "Default", "Cache");
        CreateDir(_local, "Microsoft", "Edge", "User Data", "Default", "Cache");
        CreateDir(_local, "Mozilla", "Firefox", "Profiles", "p.default", "cache2");

        var names = BrowserCacheCatalog.Build(_local, _roaming).Select(t => t.Name).ToList();

        Assert.Contains("Google Chrome", names);
        Assert.Contains("Microsoft Edge", names);
        Assert.Contains("Mozilla Firefox", names);
    }

    [Fact]
    public void Build_WithoutAnyBrowser_ReturnsEmpty()
        => Assert.Empty(BrowserCacheCatalog.Build(_local, _roaming));

    [Fact]
    public void Build_ReturnsOnlyExistingDirectories()
    {
        // Nur „Cache" existiert – die übrigen Ordnernamen dürfen nicht erfunden werden.
        CreateDir(_local, "Google", "Chrome", "User Data", "Default", "Cache");

        var chrome = Assert.Single(BrowserCacheCatalog.Build(_local, _roaming));
        Assert.All(chrome.CacheDirectories, d => Assert.True(Directory.Exists(d)));
    }

    // ---------- Zusammenspiel: leeren und nachprüfen ----------

    [Fact]
    public void ChromeCache_IsActuallyEmptyAfterClearing()
    {
        var userData = Path.Combine(_local, "Google", "Chrome", "User Data");
        var cache = CreateDir(userData, "Default", "Cache");
        WriteFile(CreateDir(cache, "Cache_Data"), "entry", 4096);
        var dawn = CreateDir(userData, "Default", "DawnCache");
        WriteFile(dawn, "shader", 2048);

        var chrome = Assert.Single(BrowserCacheCatalog.Build(_local, _roaming));

        long freed = 0, remaining = 0;
        foreach (var dir in chrome.CacheDirectories)
            freed += DirectoryCleaner.ClearContents(dir).FreedBytes;
        foreach (var dir in chrome.CacheDirectories)
            remaining += DirectoryCleaner.Size(dir);

        Assert.Equal(6144, freed);
        Assert.Equal(0, remaining);   // genau das war vorher nicht garantiert
    }
}
