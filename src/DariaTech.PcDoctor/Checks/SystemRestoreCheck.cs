using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Systemwiederherstellung: Gibt es vor Reparaturen überhaupt eine Rückfallebene?
/// Ist der Systemschutz abgeschaltet, kann Windows keine Wiederherstellungspunkte
/// anlegen – dann greift auch die automatische Sicherung dieser App vor
/// systemverändernden Aktionen ins Leere. Rein lesend.
/// </summary>
public sealed class SystemRestoreCheck : ICheck
{
    public string Area => "Systemwiederherstellung";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            var points = DriveProtectionReader.ReadRestorePoints(ct);
            var disabled = DriveProtectionReader.IsSystemRestoreDisabled();
            var evaluation = RestoreProtectionRules.Evaluate(points, DateTime.Now, disabled);

            results.Add(new CheckResult(Area, "Rückfallebene",
                evaluation.Summary,
                evaluation.Severity,
                Detail: evaluation.Detail,
                Tip: evaluation.Severity >= Severity.Warning
                    ? "Über die Reparatur „Systemwiederherstellung einschalten“ aktivieren – sie legt " +
                      "zugleich einen ersten Wiederherstellungspunkt an. Prüfen lässt sich das in " +
                      "Windows unter „Wiederherstellungspunkt erstellen“."
                    : null,
                OpenTarget: evaluation.Severity >= Severity.Warning ? "systempropertiesprotection.exe" : null));

            // Die jüngsten Punkte auflisten – hilft beim Einschätzen, worauf man zurückkann.
            foreach (var point in points.Take(5))
                results.Add(new CheckResult(Area, " – Wiederherstellungspunkt",
                    point.ToString("dd.MM.yyyy HH:mm"), Severity.Info));

            return results;
        }, ct);
}
