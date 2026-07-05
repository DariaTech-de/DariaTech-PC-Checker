using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Windows-Aktivierungsstatus über <c>SoftwareLicensingProduct</c>
/// (nur das Windows-Betriebssystemprodukt mit gültigem Teil-Product-Key).
/// Nicht aktiviert / Benachrichtigungs- oder Testzeitraum → Warnung. Rein lesend.
/// Häufig relevant bei gebraucht gekauften PCs.
/// </summary>
public sealed class WindowsActivationCheck : ICheck
{
    public string Area => "Windows-Aktivierung";

    // ApplicationId des Windows-Betriebssystemprodukts (konstant über alle Windows-Versionen).
    private const string WindowsAppId = "55c92734-d682-4d71-983e-d6ec3f16059f";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            try
            {
                var query = new ObjectQuery(
                    "SELECT Name, LicenseStatus, PartialProductKey FROM SoftwareLicensingProduct " +
                    $"WHERE ApplicationId='{WindowsAppId}' AND PartialProductKey IS NOT NULL");
                using var searcher = new ManagementObjectSearcher(query);

                var found = false;
                foreach (ManagementBaseObject obj in searcher.Get())
                {
                    ct.ThrowIfCancellationRequested();
                    found = true;

                    var status = obj["LicenseStatus"] is null ? -1 : Convert.ToInt32(obj["LicenseStatus"]);
                    var (text, sev) = Describe(status);

                    results.Add(new CheckResult(Area, "Status", text, sev,
                        Detail: sev == Severity.Ok
                            ? null
                            : "Ein nicht aktiviertes Windows zeigt ein Wasserzeichen, sperrt Teile der " +
                              "Personalisierung und weist regelmäßig auf die fehlende Aktivierung hin.",
                        Tip: sev == Severity.Ok
                            ? null
                            : "So beheben: Einstellungen → „System“ → „Aktivierung“. Dort einen gültigen " +
                              "Product-Key eingeben oder das Gerät mit einem Microsoft-Konto verknüpfen, " +
                              "das eine digitale Lizenz besitzt.",
                        OpenTarget: sev == Severity.Ok ? null : "ms-settings:activation"));
                    break;
                }

                if (!found)
                    results.Add(new CheckResult(Area, "Status", "nicht ermittelbar", Severity.Info));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new CheckResult(Area, "Status", "nicht prüfbar", Severity.Info));
            }

            return results;
        }, ct);

    /// <summary>Übersetzt den WMI-LicenseStatus in Anzeigetext + Ampel.</summary>
    internal static (string Text, Severity Severity) Describe(int status) => status switch
    {
        1 => ("aktiviert", Severity.Ok),
        0 => ("nicht lizenziert", Severity.Warning),
        2 => ("Testzeitraum (Erstinbetriebnahme)", Severity.Warning),
        3 => ("Testzeitraum (Out-of-Tolerance)", Severity.Warning),
        4 => ("nicht echt (Kulanzzeitraum)", Severity.Warning),
        5 => ("nicht aktiviert (Benachrichtigungsmodus)", Severity.Warning),
        6 => ("verlängerter Testzeitraum", Severity.Warning),
        _ => ($"unbekannt ({status})", Severity.Info)
    };
}
