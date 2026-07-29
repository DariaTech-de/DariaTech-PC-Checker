using System.IO;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>
/// Findet das Defender-Kommandozeilenwerkzeug <c>MpCmdRun.exe</c>.
///
/// Wichtig: Defender aktualisiert sich selbst in einen versionierten
/// Platform-Ordner. Die dortige Fassung ist die aktuelle; die Datei unter
/// „Program Files\Windows Defender“ kann veraltet sein oder fehlen. Deshalb wird
/// der Platform-Ordner bevorzugt und die neueste Version gewählt.
/// </summary>
public static class DefenderCli
{
    /// <summary>Pfad zu MpCmdRun.exe oder <c>null</c>, wenn Defender nicht vorhanden ist.</summary>
    public static string? FindMpCmdRun()
    {
        // 1. Aktuelle Plattformversion (bevorzugt).
        try
        {
            var platformRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Microsoft", "Windows Defender", "Platform");

            if (Directory.Exists(platformRoot))
            {
                var newest = Directory.EnumerateDirectories(platformRoot)
                    .Select(dir => new { Dir = dir, Exe = Path.Combine(dir, "MpCmdRun.exe") })
                    .Where(x => File.Exists(x.Exe))
                    .OrderByDescending(x => Path.GetFileName(x.Dir), StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (newest is not null) return newest.Exe;
            }
        }
        catch { /* weiter mit Standardpfad */ }

        // 2. Klassischer Installationspfad.
        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (string.IsNullOrEmpty(folder)) continue;
            var candidate = Path.Combine(folder, "Windows Defender", "MpCmdRun.exe");
            try { if (File.Exists(candidate)) return candidate; }
            catch { /* nächsten versuchen */ }
        }

        return null;
    }

    /// <summary>Standard-Hinweis, wenn Defender nicht gefunden wurde.</summary>
    public const string NotFoundMessage =
        "Microsoft Defender wurde auf diesem PC nicht gefunden. Ist ein anderer Virenschutz " +
        "installiert (z. B. Kaspersky, Avast, Norton), übernimmt dieser den Schutz – die Prüfung " +
        "muss dann in dessen Programm erfolgen. Als unabhängige Zweitmeinung kann der " +
        "„Microsoft Safety Scanner“ verwendet werden.";
}
