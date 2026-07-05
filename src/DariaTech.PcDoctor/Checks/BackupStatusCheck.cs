using System.IO;
using DariaTech.PcDoctor.Core;
using Microsoft.Win32;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Prüft, ob auf dem PC überhaupt eine automatische Datensicherung eingerichtet
/// ist (OneDrive-Ordnersicherung oder Windows-Dateiversionsverlauf). Ist keine
/// erkennbar → Warnung, damit der Techniker vor Reparaturen ein Backup empfiehlt.
/// Rein lesend – es wird nichts verändert.
/// </summary>
public sealed class BackupStatusCheck : ICheck
{
    public string Area => "Datensicherung";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            var anyBackup = false;

            // OneDrive-Ordnersicherung
            try
            {
                var oneDrive = DetectOneDrive();
                if (oneDrive is not null)
                {
                    anyBackup = true;
                    results.Add(new CheckResult(Area, "OneDrive", oneDrive, Severity.Ok,
                        Detail: "OneDrive ist eingerichtet. Prüfen, ob die wichtigen Ordner " +
                                "(Desktop, Dokumente, Bilder) tatsächlich synchronisiert werden.",
                        Tip: "So prüfen: OneDrive-Symbol (Wolke) → Zahnrad → „Sicherung/Backup verwalten“; " +
                             "dort sollten Desktop, Dokumente und Bilder gesichert sein."));
                }
                else
                {
                    results.Add(new CheckResult(Area, "OneDrive", "nicht eingerichtet", Severity.Info));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new CheckResult(Area, "OneDrive", "nicht prüfbar", Severity.Info));
            }

            // Windows-Dateiversionsverlauf (File History)
            try
            {
                if (FileHistoryConfigured())
                {
                    anyBackup = true;
                    results.Add(new CheckResult(Area, "Dateiversionsverlauf", "eingerichtet", Severity.Ok));
                }
                else
                {
                    results.Add(new CheckResult(Area, "Dateiversionsverlauf", "nicht eingerichtet", Severity.Info));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new CheckResult(Area, "Dateiversionsverlauf", "nicht prüfbar", Severity.Info));
            }

            // Gesamtbewertung an den Anfang stellen (fällt in der Kachel sofort auf).
            var overall = anyBackup
                ? new CheckResult(Area, "Gesamtstatus", "Datensicherung vorhanden", Severity.Ok)
                : new CheckResult(Area, "Gesamtstatus", "Keine automatische Datensicherung erkannt",
                    Severity.Warning,
                    Detail: "Weder OneDrive-Sicherung noch der Windows-Dateiversionsverlauf sind aktiv. " +
                            "Bei einem Defekt (Festplatte, Windows-Fehler) könnten persönliche Daten " +
                            "verloren gehen – vor größeren Reparaturen ein Backup einrichten.",
                    Tip: "So einrichten: externe Festplatte anschließen → Einstellungen → „Update & Sicherheit“ → " +
                         "„Sicherung“ → Laufwerk hinzufügen (Dateiversionsverlauf). Alternativ die " +
                         "OneDrive-Ordnersicherung aktivieren.",
                    OpenTarget: "ms-settings:backup");
            results.Insert(0, overall);

            return results;
        }, ct);

    /// <summary>Liefert eine Kurzbeschreibung, wenn OneDrive eingerichtet ist – sonst null.</summary>
    private static string? DetectOneDrive()
    {
        using (var accounts = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts"))
        {
            if (accounts is not null)
            {
                foreach (var name in accounts.GetSubKeyNames())
                {
                    using var acc = accounts.OpenSubKey(name);
                    if (acc?.GetValue("UserFolder") is string folder &&
                        !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                    {
                        return name.StartsWith("Business", StringComparison.OrdinalIgnoreCase)
                            ? "aktiv (Geschäftskonto)"
                            : "aktiv (persönlich)";
                    }
                }
            }
        }

        var env = Environment.GetEnvironmentVariable("OneDrive");
        return !string.IsNullOrWhiteSpace(env) && Directory.Exists(env) ? "aktiv" : null;
    }

    /// <summary>
    /// True, wenn der Dateiversionsverlauf konfiguriert ist. Die Einrichtung legt
    /// eine <c>Config*.xml</c> unter %LocalAppData%\Microsoft\Windows\FileHistory an.
    /// </summary>
    private static bool FileHistoryConfigured()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(local)) return false;
        var configDir = Path.Combine(local, "Microsoft", "Windows", "FileHistory", "Configuration");
        return Directory.Exists(configDir) && Directory.EnumerateFiles(configDir, "Config*.xml").Any();
    }
}
