using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Aktualisiert die Virensignaturen von Microsoft Defender
/// (<c>MpCmdRun.exe -SignatureUpdate</c>). Erster Schritt jeder Virenprüfung –
/// ohne aktuelle Signaturen findet auch ein Vollscan neue Schädlinge nicht.
/// </summary>
public sealed class DefenderSignatureUpdateFix : IFixAction
{
    public string Title => "Virensignaturen aktualisieren";

    public string Description =>
        "Lädt die aktuellen Virensignaturen für Microsoft Defender herunter. Das ist der erste Schritt " +
        "jeder Virenprüfung: Nur mit aktuellen Signaturen kann neue Schadsoftware überhaupt erkannt " +
        "werden. Benötigt eine Internetverbindung und dauert meist unter einer Minute. " +
        "Es wird nichts am System verändert.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;   // reines Signatur-Update

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        var mpCmdRun = DefenderCli.FindMpCmdRun();
        if (mpCmdRun is null) return new FixOutcome(false, DefenderCli.NotFoundMessage);

        progress.Report("Lade aktuelle Virensignaturen …");
        var result = await ProcessRunner.RunAsync(mpCmdRun, "-SignatureUpdate", progress, ct)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
            return new FixOutcome(false,
                $"Signatur-Update fehlgeschlagen (Code {result.ExitCode}). Internetverbindung prüfen. " +
                "Blockiert ein Schädling die Updates, hilft der Offlinescan.");

        // Nachprüfen: Sind die Signaturen jetzt wirklich aktuell?
        var status = DefenderReader.ReadStatus(ct);
        if (status.Available && status.SignatureAgeDays is int age && age > 1)
            return new FixOutcome(false,
                $"Update ausgeführt, die Signaturen sind aber weiterhin {age} Tage alt. " +
                "Das deutet darauf hin, dass die Aktualisierung blockiert wird – bitte Offlinescan " +
                "durchführen und die hosts-Datei prüfen.");

        progress.Report("Virensignaturen sind aktuell.");
        return new FixOutcome(true, "Virensignaturen wurden aktualisiert.");
    }
}
