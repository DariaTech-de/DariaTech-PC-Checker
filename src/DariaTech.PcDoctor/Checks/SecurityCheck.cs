using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Windows-Sicherheit (Defender): liest <c>MSFT_MpComputerStatus</c>
/// (root\Microsoft\Windows\Defender). Echtzeitschutz aus → Kritisch;
/// Virensignaturen &gt; 7 Tage alt → Warnung.
/// </summary>
public sealed class SecurityCheck : ICheck
{
    public string Area => "Windows-Sicherheit";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Defender");
                var query = new ObjectQuery(
                    "SELECT RealTimeProtectionEnabled, AntivirusSignatureLastUpdated FROM MSFT_MpComputerStatus");
                using var searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementBaseObject mp in searcher.Get())
                {
                    var realtime = mp["RealTimeProtectionEnabled"] is bool b && b;
                    results.Add(realtime
                        ? new CheckResult(Area, "Echtzeitschutz", "aktiv", Severity.Ok)
                        : new CheckResult(Area, "Echtzeitschutz", "AUS", Severity.Critical,
                            "Defender-Echtzeitschutz ist deaktiviert.",
                            Tip: "So beheben: Windows-Sicherheit öffnen → „Viren- & Bedrohungsschutz“ → " +
                                 "„Einstellungen verwalten“ und „Echtzeitschutz“ einschalten. " +
                                 "Ist ein anderer Virenschutz (z. B. Avast, Norton) installiert, übernimmt dieser " +
                                 "den Schutz – dann ist die Meldung in Ordnung.",
                            OpenTarget: "windowsdefender://threat"));

                    var updated = ToDate(mp["AntivirusSignatureLastUpdated"]);
                    if (updated is not null)
                    {
                        var age = (DateTime.Now - updated.Value).Days;
                        results.Add(age > 7
                            ? new CheckResult(Area, "Signaturen", $"{age} Tage alt", Severity.Warning,
                                $"Virensignaturen {age} Tage alt – aktualisieren.",
                                Tip: "So beheben: Windows-Sicherheit öffnen → „Viren- & Bedrohungsschutz“ → " +
                                     "unter „Schutzupdates“ auf „Nach Updates suchen“ klicken.",
                                OpenTarget: "windowsdefender://threat")
                            : new CheckResult(Area, "Signaturen", $"aktuell ({age} Tage)", Severity.Ok));
                    }
                    break;
                }
            }
            catch
            {
                results.Add(new CheckResult(Area, "Hinweis",
                    "Defender-Status nicht abrufbar (evtl. Drittanbieter-AV)", Severity.Info));
            }

            return results;
        }, ct);

    private static DateTime? ToDate(object? value)
    {
        if (value is null) return null;
        try { return ManagementDateTimeConverter.ToDateTime(value.ToString()); }
        catch { return null; }
    }
}
