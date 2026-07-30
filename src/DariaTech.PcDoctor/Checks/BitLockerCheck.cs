using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Laufwerksverschlüsselung (BitLocker) – geprüft aus Sicht der Reparatur-
/// Sicherheit. Die wichtigste Frage lautet nicht „ist verschlüsselt?“, sondern
/// „gibt es einen Wiederherstellungsschlüssel?“. Fehlt er, können systemnahe
/// Eingriffe dazu führen, dass der Kunde dauerhaft nicht mehr an seine Daten
/// kommt – deshalb wird genau dieser Fall als kritisch gemeldet.
///
/// Rein lesend; der Schlüssel selbst wird hier NICHT ausgelesen.
/// </summary>
public sealed class BitLockerCheck : ICheck
{
    public string Area => "Laufwerksverschlüsselung (BitLocker)";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            var volumes = DriveProtectionReader.ReadBitLocker(ct);

            if (volumes.Count == 0)
            {
                results.Add(new CheckResult(Area, "Status",
                    "BitLocker nicht verfügbar oder nicht eingerichtet", Severity.Info,
                    Detail: "Auf Windows Home ist BitLocker nicht enthalten. Die Daten sind dann " +
                            "unverschlüsselt – bei Verlust oder Diebstahl des Geräts sind sie lesbar.",
                    Tip: "Bei Notebooks mit Kundendaten lohnt sich Verschlüsselung. Windows Pro bietet " +
                         "BitLocker; bei Home gibt es teils die „Geräteverschlüsselung“ in den " +
                         "Einstellungen unter „Datenschutz & Sicherheit“."));
                return results;
            }

            var encrypted = volumes.Where(BitLockerRules.IsEncrypted).ToList();
            var withoutKey = encrypted.Where(v => !v.HasRecoveryPassword).ToList();

            // Gesamtbewertung zuerst – der Techniker soll das Risiko sofort sehen.
            if (withoutKey.Count > 0)
            {
                results.Add(new CheckResult(Area, "Gesamtbewertung",
                    $"{withoutKey.Count} verschlüsselte(s) Laufwerk(e) OHNE Wiederherstellungsschlüssel",
                    Severity.Critical,
                    Detail: "Vor systemnahen Reparaturen besteht hier ein echtes Datenverlustrisiko: " +
                            "Verlangt Windows nach einem Eingriff den Schlüssel, gibt es ohne ihn keinen " +
                            "Zugang zu den Daten – auch für uns nicht.",
                    Tip: "Zuerst die Reparatur „BitLocker-Wiederherstellungsschlüssel prüfen“ ausführen " +
                         "und den Schlüssel beim Kunden sichern (Microsoft-Konto, Ausdruck, sicherer Ort). " +
                         "Erst danach mit Reparaturen fortfahren.",
                    OpenTarget: "ms-settings:deviceencryption"));
            }
            else if (encrypted.Count > 0)
            {
                results.Add(new CheckResult(Area, "Gesamtbewertung",
                    $"{encrypted.Count} Laufwerk(e) verschlüsselt, Wiederherstellungsschlüssel vorhanden",
                    Severity.Ok,
                    Detail: "Die Verschlüsselung ist aktiv und es existiert ein Wiederherstellungsschlüssel.",
                    Tip: "Vor systemnahen Reparaturen dennoch kurz prüfen, ob der Kunde den Schlüssel " +
                         "auch selbst griffbereit hat."));
            }
            else
            {
                results.Add(new CheckResult(Area, "Gesamtbewertung",
                    "keine Laufwerke verschlüsselt", Severity.Info));
            }

            foreach (var volume in volumes)
            {
                ct.ThrowIfCancellationRequested();
                var keyNote = BitLockerRules.IsEncrypted(volume)
                    ? volume.HasRecoveryPassword
                        ? " · Wiederherstellungsschlüssel vorhanden"
                        : " · KEIN Wiederherstellungsschlüssel"
                    : string.Empty;

                results.Add(new CheckResult(Area,
                    $" – Laufwerk {volume.DriveLetter}",
                    BitLockerRules.StatusText(volume) + keyNote,
                    BitLockerRules.SeverityFor(volume),
                    Detail: BitLockerRules.AdviceFor(volume)));
            }

            return results;
        }, ct);
}
