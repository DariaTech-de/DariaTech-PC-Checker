using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Treiber &amp; Geräte (<c>Win32_PnPEntity</c>): listet Geräte mit
/// <c>ConfigManagerErrorCode != 0</c> als Problemgeräte (Warnung).
/// </summary>
public sealed class DriverDeviceCheck : ICheck
{
    public string Area => "Treiber & Geräte";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            var problems = new List<(string Name, int Code)>();

            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
            {
                foreach (ManagementBaseObject obj in searcher.Get())
                {
                    ct.ThrowIfCancellationRequested();
                    var name = obj["Name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var code = obj["ConfigManagerErrorCode"] is null ? 0 : Convert.ToInt32(obj["ConfigManagerErrorCode"]);
                    problems.Add((name!, code));
                }
            }

            if (problems.Count == 0)
            {
                results.Add(new CheckResult(Area, "Status",
                    "alle Geräte in Ordnung", Severity.Ok));
            }
            else
            {
                foreach (var (name, code) in problems)
                    results.Add(new CheckResult(Area, "Problem", name, Severity.Warning,
                        $"Geräte-Manager-Code {code}: {CodeMeaning(code)}",
                        Tip: "So beheben: Geräte-Manager öffnen, das gelb markierte Gerät suchen, " +
                             "Rechtsklick → „Treiber aktualisieren“. Hilft das nicht: Rechtsklick → " +
                             "„Gerät deinstallieren“ und den PC neu starten – Windows lädt den Treiber dann neu.",
                        OpenTarget: "devmgmt.msc"));
                results.Add(new CheckResult(Area, "Zusammenfassung",
                    $"{problems.Count} Gerät(e) mit Treiberproblem", Severity.Warning,
                    $"{problems.Count} Gerät(e) mit Treiberproblem im Geräte-Manager – Treiber neu installieren/aktualisieren.",
                    Tip: "So beheben: Geräte-Manager öffnen und die gelb markierten Geräte prüfen " +
                         "(Rechtsklick → „Treiber aktualisieren“). Aktuelle Treiber gibt es auch auf der " +
                         "Hersteller-Website des PCs bzw. der Hardware.",
                    OpenTarget: "devmgmt.msc"));
            }

            return results;
        }, ct);

    /// <summary>Klartext zu den gängigsten Geräte-Manager-Fehlercodes.</summary>
    private static string CodeMeaning(int code) => code switch
    {
        1 => "Gerät nicht korrekt konfiguriert.",
        3 => "Treiber beschädigt oder zu wenig Arbeitsspeicher.",
        10 => "Gerät kann nicht gestartet werden – Treiber prüfen.",
        12 => "Nicht genügend freie Ressourcen.",
        18 => "Treiber neu installieren.",
        19 => "Registry-Konfiguration beschädigt.",
        22 => "Gerät ist deaktiviert.",
        24 => "Gerät nicht vorhanden/fehlerhaft.",
        28 => "Treiber nicht installiert.",
        31 => "Windows kann keinen passenden Treiber laden.",
        37 => "Treiber meldet einen Fehler.",
        39 => "Treiber beschädigt oder fehlt.",
        43 => "Hardware meldet ein Problem (defekt?).",
        45 => "Gerät derzeit nicht angeschlossen.",
        52 => "Treibersignatur nicht überprüfbar.",
        _ => "Treiber prüfen/neu installieren."
    };
}
