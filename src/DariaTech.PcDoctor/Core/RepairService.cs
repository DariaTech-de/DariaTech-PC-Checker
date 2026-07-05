using DariaTech.PcDoctor.Infrastructure;
using DariaTech.PcDoctor.Models;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Führt eine <see cref="IFixAction"/> sicher aus: legt (falls von der Aktion
/// verlangt) zuerst einen Systemwiederherstellungspunkt an, protokolliert den
/// kompletten Ablauf und meldet den Fortschritt live an die UI.
///
/// Die Nutzerbestätigung selbst erfolgt in der UI-Schicht; der RepairService
/// geht davon aus, dass die Aktion bereits bestätigt wurde.
/// </summary>
public sealed class RepairService
{
    private readonly RestorePointService _restorePoints;
    private readonly ILogger<RepairService> _log;

    public RepairService(RestorePointService restorePoints, ILogger<RepairService> log)
    {
        _restorePoints = restorePoints;
        _log = log;
    }

    public async Task<FixOutcome> RunAsync(
        IFixAction fix,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        _log.LogInformation("Starte Reparatur: {Title}", fix.Title);
        progress.Report($"Starte: {fix.Title}");

        if (fix.RequiresRestorePoint)
        {
            progress.Report("Lege Systemwiederherstellungspunkt an …");
            var rp = await _restorePoints
                .CreateAsync($"{CompanyInfo.ProductFull}: {fix.Title}", ct)
                .ConfigureAwait(false);

            progress.Report(rp.Message);
            if (rp.Success)
                _log.LogInformation("Wiederherstellungspunkt angelegt für {Title}", fix.Title);
            else
                _log.LogWarning("Kein Wiederherstellungspunkt für {Title}: {Msg}", fix.Title, rp.Message);
        }

        try
        {
            var outcome = await fix.ExecuteAsync(progress, ct).ConfigureAwait(false);
            if (outcome.Success)
                _log.LogInformation("Reparatur erfolgreich: {Title} – {Msg}", fix.Title, outcome.Message);
            else
                _log.LogWarning("Reparatur fehlgeschlagen: {Title} – {Msg}", fix.Title, outcome.Message);

            progress.Report(outcome.Message);
            return outcome;
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("Reparatur abgebrochen: {Title}", fix.Title);
            progress.Report("Abgebrochen.");
            return new FixOutcome(false, "Vorgang abgebrochen.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Fehler bei Reparatur: {Title}", fix.Title);
            progress.Report($"Fehler: {ex.Message}");
            return new FixOutcome(false, $"Fehler: {ex.Message}");
        }
    }
}
