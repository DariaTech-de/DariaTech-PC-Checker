using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Startet die Windows-Audiodienste neu (<c>Audiosrv</c> und
/// <c>AudioEndpointBuilder</c>). Behebt das häufige „plötzlich kein Ton mehr“,
/// ohne dass ein Neustart nötig ist.
///
/// Reihenfolge ist wichtig: AudioEndpointBuilder muss vor Audiosrv laufen,
/// da Audiosrv davon abhängt.
/// </summary>
public sealed class RestartAudioServiceFix : IFixAction
{
    public string Title => "Audiodienst neu starten";

    public string Description =>
        "Startet die Windows-Audiodienste neu. Hilft, wenn plötzlich kein Ton mehr kommt, das " +
        "Lautsprechersymbol ein rotes Kreuz zeigt oder Mikrofon/Kopfhörer nicht erkannt werden. " +
        "Es werden keine Einstellungen verändert und keine Daten gelöscht – die Dienste werden nur " +
        "neu gestartet. Laufende Wiedergabe (z. B. ein Video) wird dabei kurz unterbrochen.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;   // reiner Dienst-Neustart

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        // Stoppen in umgekehrter Abhängigkeitsreihenfolge …
        progress.Report("Stoppe Audiodienste …");
        await ProcessRunner.RunAsync("net.exe", "stop Audiosrv /y", progress, ct).ConfigureAwait(false);
        await ProcessRunner.RunAsync("net.exe", "stop AudioEndpointBuilder /y", progress, ct).ConfigureAwait(false);

        // … und in richtiger Reihenfolge wieder starten.
        progress.Report("Starte Audiodienste …");
        var endpoint = await ProcessRunner.RunAsync("net.exe", "start AudioEndpointBuilder", progress, ct)
            .ConfigureAwait(false);
        var audio = await ProcessRunner.RunAsync("net.exe", "start Audiosrv", progress, ct)
            .ConfigureAwait(false);

        if (audio.ExitCode == 0 && endpoint.ExitCode == 0)
        {
            const string ok = "Audiodienste neu gestartet. Bitte den Ton testen – kommt weiterhin nichts, " +
                              "im Lautsprechersymbol das richtige Wiedergabegerät auswählen und " +
                              "anschließend den Audiotreiber neu installieren.";
            progress.Report(ok);
            return new FixOutcome(true, ok);
        }

        return new FixOutcome(false,
            "Mindestens ein Audiodienst konnte nicht gestartet werden. Bitte den PC neu starten – " +
            "die Dienste starten dann automatisch.");
    }
}
