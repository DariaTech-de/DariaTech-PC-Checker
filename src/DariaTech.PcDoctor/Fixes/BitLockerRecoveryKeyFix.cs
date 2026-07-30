using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Prüft vor Reparaturen, ob für verschlüsselte Laufwerke ein
/// Wiederherstellungsschlüssel existiert, und zeigt ihn dem Techniker an, damit
/// er beim Kunden gesichert werden kann.
///
/// Umgang mit dem Schlüssel (bewusst so gelöst):
/// - Er erscheint AUSSCHLIESSLICH im Live-Protokoll auf dem Bildschirm.
/// - Er wird NICHT in die Ergebnismeldung geschrieben – diese landet in der
///   Protokolldatei und im Kundenbericht.
/// - Er wird nirgends gespeichert; die App legt keine Datei damit an.
/// So bleibt der Schlüssel dort, wo er hingehört: beim Kunden.
/// </summary>
public sealed class BitLockerRecoveryKeyFix : IFixAction
{
    public string Title => "BitLocker-Wiederherstellungsschlüssel prüfen";

    public string Description =>
        "Prüft für alle verschlüsselten Laufwerke, ob ein Wiederherstellungsschlüssel hinterlegt ist, " +
        "und zeigt ihn im Protokollfenster an. Dieser Schlüssel ist zwingend nötig, falls Windows nach " +
        "einer systemnahen Reparatur (Startumgebung, Firmware, Klonen) danach fragt – ohne ihn sind die " +
        "Daten dauerhaft unzugänglich.\n\n" +
        "WICHTIG: Der Schlüssel ist vertraulich. Er wird nur am Bildschirm angezeigt, NICHT gespeichert " +
        "und NICHT in den Kundenbericht oder die Protokolldatei übernommen. Bitte gemeinsam mit dem " +
        "Kunden sichern (Microsoft-Konto, Ausdruck oder sicher verwahrte Datei) und den Bildschirm " +
        "danach nicht unbeaufsichtigt lassen.\n\n" +
        "Es wird nichts am System verändert – reine Abfrage.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;   // reine Abfrage

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        var volumes = DriveProtectionReader.ReadBitLocker(ct);
        var encrypted = volumes.Where(BitLockerRules.IsEncrypted).ToList();

        if (encrypted.Count == 0)
        {
            const string none = "Es ist kein Laufwerk verschlüsselt – ein Wiederherstellungsschlüssel " +
                                "wird nicht benötigt.";
            progress.Report(none);
            return new FixOutcome(true, none);
        }

        var missing = new List<string>();
        var found = 0;

        foreach (var volume in encrypted)
        {
            ct.ThrowIfCancellationRequested();

            if (!volume.HasRecoveryPassword)
            {
                missing.Add(volume.DriveLetter);
                progress.Report($"⚠ {volume.DriveLetter} ist verschlüsselt, hat aber KEINEN " +
                                "Wiederherstellungsschlüssel.");
                continue;
            }

            found++;
            progress.Report($"— Wiederherstellungsschlüssel für {volume.DriveLetter} " +
                            "(vertraulich, bitte nicht weitergeben):");

            // Ausgabe geht nur in das Live-Protokoll am Bildschirm.
            await ProcessRunner.RunAsync(
                "manage-bde.exe",
                $"-protectors -get {volume.DriveLetter} -Type RecoveryPassword",
                progress, ct).ConfigureAwait(false);
        }

        if (missing.Count > 0)
        {
            // Bewusst OHNE Schlüsselmaterial: diese Meldung wird protokolliert.
            var msg = $"Für {string.Join(", ", missing)} ist KEIN Wiederherstellungsschlüssel hinterlegt. " +
                      "Vor systemnahen Reparaturen unbedingt einen anlegen: Windows-Einstellungen → " +
                      "„Datenschutz & Sicherheit“ → „Geräteverschlüsselung/BitLocker“ → " +
                      "„Wiederherstellungsschlüssel sichern“.";
            progress.Report(msg);
            SystemLauncher.Open("ms-settings:deviceencryption");
            return new FixOutcome(false, msg);
        }

        var ok = $"Für {found} verschlüsselte(s) Laufwerk(e) ist ein Wiederherstellungsschlüssel " +
                 "vorhanden und wurde am Bildschirm angezeigt. Bitte jetzt gemeinsam mit dem Kunden " +
                 "sichern – der Schlüssel wurde weder gespeichert noch protokolliert.";
        progress.Report(ok);
        return new FixOutcome(true, ok);
    }
}
