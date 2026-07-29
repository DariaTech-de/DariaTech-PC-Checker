using System.IO;
using System.Net.Http;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Zweitmeinung mit dem <b>Microsoft Safety Scanner</b> (MSERT): ein offizieller,
/// eigenständiger Scanner von Microsoft mit eigener Signaturbasis. Sinnvoll, wenn
/// Defender nichts findet, der PC sich aber auffällig verhält – oder wenn ein
/// Drittanbieter-Virenschutz installiert ist.
///
/// Der Scanner wird bei jedem Lauf frisch geladen, weil seine Signaturen nach
/// zehn Tagen ablaufen. Nach dem Scan wird das Protokoll ausgewertet, damit das
/// Ergebnis konkret benannt werden kann.
/// </summary>
public sealed class SafetyScannerFix : IFixAction
{
    /// <summary>Offizieller Microsoft-Downloadlink (64 Bit).</summary>
    private const string DownloadUrl = "https://go.microsoft.com/fwlink/?LinkId=212732";

    public string Title => "Microsoft Safety Scanner (Zweitmeinung)";

    public string Description =>
        "Lädt den Microsoft Safety Scanner – einen offiziellen, eigenständigen Virenscanner von " +
        "Microsoft – und führt damit einen vollständigen Scan durch. Gefundene Schadsoftware wird " +
        "entfernt. Sinnvoll als unabhängige Zweitmeinung, wenn Defender nichts findet, der PC sich " +
        "aber verdächtig verhält.\n\n" +
        "Hinweise:\n" +
        "• Benötigt eine Internetverbindung; der Download ist etwa 130–150 MB groß.\n" +
        "• Der Scan dauert je nach Datenmenge 30 Minuten bis mehrere Stunden.\n" +
        "• Der Scanner wird jedes Mal neu geladen, weil seine Signaturen nach 10 Tagen ablaufen.\n" +
        "• Er wird nach dem Lauf wieder entfernt und installiert sich nicht dauerhaft.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;   // gefundene Schadsoftware wird entfernt

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        var exePath = Path.Combine(Path.GetTempPath(), "DariaTech-msert.exe");

        // 1. Herunterladen
        try
        {
            progress.Report("Lade Microsoft Safety Scanner (ca. 130–150 MB) …");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
            using var response = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var target = File.Create(exePath))
            await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            {
                await source.CopyToAsync(target, ct).ConfigureAwait(false);
            }

            var sizeMb = new FileInfo(exePath).Length / 1024d / 1024d;
            progress.Report($"Download abgeschlossen ({sizeMb:N0} MB).");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            TryDelete(exePath);
            return new FixOutcome(false,
                $"Der Safety Scanner konnte nicht geladen werden: {ex.Message}. " +
                "Internetverbindung prüfen. Alternativ manuell laden: " +
                "https://www.microsoft.com/security/scanner");
        }

        // 2. Scannen (still, vollständig, mit Bereinigung)
        int exitCode;
        try
        {
            progress.Report("Starte vollständigen Scan – das dauert längere Zeit …");
            var scan = await ProcessRunner.RunAsync(exePath, "/Q /F:Y", progress, ct).ConfigureAwait(false);
            exitCode = scan.ExitCode;
        }
        catch (OperationCanceledException)
        {
            TryDelete(exePath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(exePath);
            return new FixOutcome(false, $"Der Scan konnte nicht ausgeführt werden: {ex.Message}");
        }

        // 3. Protokoll auswerten und aufräumen
        var result = MsertLogParser.Parse(ReadLog());
        TryDelete(exePath);

        if (result.FoundThreats)
        {
            var names = string.Join(", ", result.ThreatNames.Take(8));
            var msg = $"Der Safety Scanner hat {result.ThreatNames.Count} Bedrohung(en) gefunden und " +
                      $"entfernt: {names}. Bitte anschließend einen Defender-Vollscan ausführen und " +
                      "den PC neu starten.";
            progress.Report(msg);
            return new FixOutcome(true, msg);
        }

        var clean = exitCode == 0
            ? "Der Safety Scanner hat keine Schadsoftware gefunden (unabhängige Zweitmeinung)."
            : $"Scan beendet (Code {exitCode}) – im Protokoll wurden keine Bedrohungen ausgewiesen. " +
              $"Protokoll: {LogPath}";
        progress.Report(clean);
        return new FixOutcome(true, clean);
    }

    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "debug", "msert.log");

    private static string? ReadLog()
    {
        try { return File.Exists(LogPath) ? File.ReadAllText(LogPath) : null; }
        catch { return null; }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* Aufräumen ist best effort */ }
    }
}
