using System.IO;

namespace DariaTech.PcDoctor.Core;

/// <summary>Ergebnis einer Aufräumaktion.</summary>
/// <param name="FreedBytes">Tatsächlich freigegebene Bytes.</param>
/// <param name="Deleted">Anzahl gelöschter Dateien/Ordner.</param>
/// <param name="Skipped">Anzahl gesperrter/nicht löschbarer Objekte (Programm läuft).</param>
public sealed record CleanupResult(long FreedBytes, int Deleted, int Skipped)
{
    public static readonly CleanupResult Empty = new(0, 0, 0);

    public CleanupResult Add(CleanupResult other)
        => new(FreedBytes + other.FreedBytes, Deleted + other.Deleted, Skipped + other.Skipped);
}

/// <summary>
/// Leert Verzeichnisse und misst deren Größe. Bewusst ohne Windows-Spezifika,
/// damit die Logik plattformunabhängig testbar ist.
///
/// Der Ordner selbst bleibt erhalten – geleert wird nur der Inhalt. Gesperrte
/// Objekte werden gezählt statt zu werfen: bei laufenden Programmen ist das der
/// Normalfall und muss dem Nutzer ehrlich gemeldet werden.
/// </summary>
public static class DirectoryCleaner
{
    /// <summary>Löscht den Inhalt eines Verzeichnisses (rekursiv), ohne den Ordner selbst.</summary>
    public static CleanupResult ClearContents(string directory, CancellationToken ct = default)
    {
        if (!Directory.Exists(directory)) return CleanupResult.Empty;

        long freed = 0;
        int deleted = 0, skipped = 0;

        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(directory).ToList(); }
        catch { return CleanupResult.Empty; }

        foreach (var path in entries)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(path))
                {
                    var size = SafeFileSize(path);
                    TryClearReadOnly(path);
                    File.Delete(path);
                    freed += size;
                    deleted++;
                }
                else if (Directory.Exists(path))
                {
                    var size = Size(path, ct);
                    Directory.Delete(path, recursive: true);
                    freed += size;
                    deleted++;
                }
            }
            catch
            {
                // Gesperrt (Programm läuft) oder kein Zugriff -> ehrlich zählen.
                skipped++;
            }
        }

        return new CleanupResult(freed, deleted, skipped);
    }

    /// <summary>Gesamtgröße eines Verzeichnisses in Bytes (nicht lesbare Teile werden übersprungen).</summary>
    public static long Size(string directory, CancellationToken ct = default)
    {
        if (!Directory.Exists(directory)) return 0;

        long total = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint   // Verknüpfungen nicht verfolgen
        };

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", options))
            {
                ct.ThrowIfCancellationRequested();
                total += SafeFileSize(file);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Verzeichnis verschwunden/nicht lesbar */ }

        return total;
    }

    private static long SafeFileSize(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    private static void TryClearReadOnly(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
        catch { /* nicht kritisch – Löschen wird es zeigen */ }
    }
}
