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

    /// <param name="confirmWithoutRestorePoint">
    /// Wird gefragt, wenn die Aktion einen Wiederherstellungspunkt verlangt, dieser
    /// aber nicht angelegt werden konnte. Liefert die Rückfrage <c>false</c> – oder
    /// ist keine hinterlegt –, wird die Reparatur NICHT ausgeführt. Ohne
    /// Rückfallebene wird nichts am System verändert, ohne dass der Techniker das
    /// ausdrücklich entschieden hat.
    /// </param>
    public async Task<FixOutcome> RunAsync(
        IFixAction fix,
        IProgress<string> progress,
        CancellationToken ct = default,
        Func<string, bool>? confirmWithoutRestorePoint = null)
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
            {
                _log.LogInformation("Wiederherstellungspunkt angelegt für {Title}", fix.Title);
            }
            else
            {
                _log.LogWarning("Kein Wiederherstellungspunkt für {Title}: {Msg}", fix.Title, rp.Message);

                // Die Reparatur wurde mit der Zusage bestätigt, dass vorher ein
                // Wiederherstellungspunkt angelegt wird. Klappt das nicht, darf sie
                // nicht stillschweigend trotzdem laufen – sonst gibt es keinen Weg
                // zurück, wenn der Eingriff schiefgeht.
                var proceed = confirmWithoutRestorePoint?.Invoke(rp.Message) ?? false;
                if (!proceed)
                {
                    const string aborted =
                        "Abgebrochen: Es konnte kein Systemwiederherstellungspunkt angelegt werden. " +
                        "Am System wurde nichts verändert. Zuerst „Systemwiederherstellung einschalten“ " +
                        "ausführen – danach ist diese Reparatur abgesichert.";
                    _log.LogWarning("Reparatur ohne Wiederherstellungspunkt abgelehnt: {Title}", fix.Title);
                    progress.Report(aborted);
                    return new FixOutcome(false, aborted);
                }

                _log.LogWarning("Reparatur läuft OHNE Wiederherstellungspunkt (bestätigt): {Title}", fix.Title);
                progress.Report("Fortsetzung ohne Wiederherstellungspunkt – ausdrücklich bestätigt.");
            }
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
