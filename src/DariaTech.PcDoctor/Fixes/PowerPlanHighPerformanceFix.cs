using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Aktiviert den Energiesparplan „Höchstleistung" und schaltet den
/// Schnellstart (Fast Startup) ab – hilft bei trägem Verhalten und
/// Aufwach-/Treiberproblemen nach dem Ruhezustand.
/// </summary>
public sealed class PowerPlanHighPerformanceFix : IFixAction
{
    // GUID des integrierten Plans „Höchstleistung".
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public string Title => "Energieplan: Höchstleistung + Schnellstart aus";
    public string Description =>
        "Stellt den Energieplan auf „Höchstleistung“ und deaktiviert den " +
        "Windows-Schnellstart. Hilft bei trägem System und Aufwachproblemen.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Aktiviere Höchstleistung …");
        var setPlan = await ProcessRunner.RunAsync("powercfg.exe", $"/setactive {HighPerformance}", progress, ct)
            .ConfigureAwait(false);

        progress.Report("Deaktiviere Schnellstart …");
        await ProcessRunner.RunAsync("powercfg.exe", "/hibernate off", progress, ct).ConfigureAwait(false);

        return setPlan.ExitCode == 0
            ? new FixOutcome(true, "Energieplan auf Höchstleistung gesetzt, Schnellstart deaktiviert.")
            : new FixOutcome(true,
                "Schnellstart deaktiviert. Höchstleistungs-Plan evtl. nicht vorhanden – " +
                "in den Energieoptionen prüfen.");
    }
}
