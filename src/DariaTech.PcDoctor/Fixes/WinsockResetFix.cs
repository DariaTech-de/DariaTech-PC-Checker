using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Setzt den Winsock-Katalog zurück (<c>netsh winsock reset</c>). Behebt
/// hartnäckige Netzwerkprobleme; ein <b>Neustart</b> ist anschließend nötig.
/// Vorher Wiederherstellungspunkt + Bestätigung.
/// </summary>
public sealed class WinsockResetFix : IFixAction
{
    public string Title => "Netzwerk-Reset (Winsock)";
    public string Description =>
        "Setzt den Winsock-Katalog zurück (netsh winsock reset). Behebt hartnäckige " +
        "Verbindungsprobleme. Achtung: Danach ist ein Neustart des PCs erforderlich.";
    public bool RequiresRestorePoint => true;
    public bool IsReversible => false;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Setze Winsock zurück …");
        var result = await ProcessRunner.RunAsync("netsh.exe", "winsock reset", progress, ct)
            .ConfigureAwait(false);

        return result.ExitCode == 0
            ? new FixOutcome(true, "Winsock zurückgesetzt. Bitte den PC neu starten.")
            : new FixOutcome(false, $"Winsock-Reset fehlgeschlagen (Code {result.ExitCode}).");
    }
}
