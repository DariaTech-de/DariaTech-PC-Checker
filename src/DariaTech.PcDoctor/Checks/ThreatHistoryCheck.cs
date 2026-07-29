using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Bedrohungshistorie von Microsoft Defender: welche Schadsoftware wurde erkannt,
/// wie schwer war sie und wurde sie tatsächlich beseitigt? Erkennungen, die noch
/// Handlungsbedarf haben (nur „erkannt“, „zugelassen“, „Entfernen fehlgeschlagen“)
/// werden als kritisch gemeldet – das ist der wichtigste Punkt einer
/// Virenprüfung, weil ein zugelassener Trojaner weiterhin aktiv ist.
///
/// Die Erkennung selbst leistet Microsoft Defender mit echten, täglich
/// aktualisierten Signaturen – wir lesen und bewerten das Ergebnis und
/// dokumentieren es im Kundenbericht.
/// </summary>
public sealed class ThreatHistoryCheck : ICheck
{
    public string Area => "Schadsoftware-Befunde";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();

            var status = DefenderReader.ReadStatus(ct);
            if (!status.Available)
            {
                results.Add(new CheckResult(Area, "Hinweis",
                    "Defender-Historie nicht abrufbar (evtl. Drittanbieter-Virenschutz aktiv)",
                    Severity.Info,
                    Detail: "Ist ein anderer Virenschutz installiert, führt dieser die Prüfung durch. " +
                            "Die Befunde stehen dann in dessen eigenem Programm.",
                    Tip: "Bei Verdacht auf Befall zusätzlich den „Microsoft Safety Scanner“ als " +
                         "Zweitmeinung laufen lassen (Reparatur in dieser Kachel)."));
                return results;
            }

            var threats = DefenderReader.ReadThreats(ct);

            if (threats.Count == 0)
            {
                results.Add(new CheckResult(Area, "Befunde",
                    "keine Schadsoftware in der Defender-Historie", Severity.Ok,
                    Detail: "Microsoft Defender hat auf diesem PC bisher keine Bedrohung protokolliert."));
                return results;
            }

            var unresolved = threats.Where(t => ThreatStatusMapper.NeedsAction(t.StatusId)).ToList();
            var handled = threats.Count - unresolved.Count;

            results.Add(new CheckResult(Area, "Befunde insgesamt",
                $"{threats.Count} Erkennung(en), davon {unresolved.Count} offen",
                ThreatStatusMapper.Overall(threats),
                Detail: unresolved.Count > 0
                    ? $"{unresolved.Count} Bedrohung(en) sind NICHT sicher beseitigt – hier besteht " +
                      $"Handlungsbedarf. {handled} Erkennung(en) wurden bereits behandelt."
                    : $"Alle {handled} Erkennung(en) wurden von Defender behandelt (bereinigt, " +
                      "in Quarantäne oder blockiert).",
                Tip: unresolved.Count > 0
                    ? "So beheben: In dieser Kachel „Erkannte Bedrohungen entfernen“ ausführen, danach " +
                      "„Defender-Vollscan“. Bleibt etwas offen, „Defender-Offlinescan“ (startet neu und " +
                      "entfernt tief verankerte Schädlinge) und zusätzlich den „Microsoft Safety Scanner“ " +
                      "als Zweitmeinung."
                    : null,
                OpenTarget: unresolved.Count > 0 ? "windowsdefender://threat" : null));

            // Offene Bedrohungen zuerst und vollständig auflisten – die dürfen nicht untergehen.
            foreach (var threat in unresolved.Take(15))
                results.Add(Row(threat));

            // Behandelte Funde zur Dokumentation (begrenzt, damit der Bericht lesbar bleibt).
            foreach (var threat in threats.Where(t => !ThreatStatusMapper.NeedsAction(t.StatusId)).Take(10))
                results.Add(Row(threat));

            return results;
        }, ct);

    private CheckResult Row(ThreatRecord threat)
        => new(Area,
            $" – {threat.Name}",
            ThreatStatusMapper.Describe(threat),
            ThreatStatusMapper.SeverityFor(threat),
            Detail: string.IsNullOrWhiteSpace(threat.Resource)
                ? null
                : $"Betroffen: {threat.Resource}",
            Tip: ThreatStatusMapper.NeedsAction(threat.StatusId)
                ? "Diese Bedrohung ist noch nicht beseitigt. Über „Erkannte Bedrohungen entfernen“ " +
                  "behandeln und anschließend mit einem Vollscan prüfen."
                : null);
}
