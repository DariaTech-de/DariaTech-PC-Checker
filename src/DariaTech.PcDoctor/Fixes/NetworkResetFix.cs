using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Vollständiger Netzwerk-Reset: DNS-Cache leeren, IP freigeben/erneuern,
/// Winsock und TCP/IP-Stack zurücksetzen. Behebt hartnäckige Verbindungsprobleme.
/// <b>Neustart</b> danach nötig. Wiederherstellungspunkt + Bestätigung.
/// </summary>
public sealed class NetworkResetFix : IFixAction
{
    public string Title => "Netzwerk komplett zurücksetzen";
    public string Description =>
        "Setzt das Netzwerk umfassend zurück: DNS leeren, IP-Adresse neu beziehen, " +
        "Winsock und TCP/IP-Stack zurücksetzen. Achtung: Danach ist ein Neustart nötig.";
    public bool RequiresRestorePoint => true;
    public bool IsReversible => false;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        var steps = new (string File, string Args)[]
        {
            ("ipconfig.exe", "/flushdns"),
            ("ipconfig.exe", "/release"),
            ("ipconfig.exe", "/renew"),
            ("netsh.exe", "winsock reset"),
            ("netsh.exe", "int ip reset")
        };

        var hadError = false;
        foreach (var (file, args) in steps)
        {
            ct.ThrowIfCancellationRequested();
            progress.Report($"{System.IO.Path.GetFileNameWithoutExtension(file)} {args} …");
            var result = await ProcessRunner.RunAsync(file, args, progress, ct).ConfigureAwait(false);
            if (result.ExitCode != 0) hadError = true;
        }

        return new FixOutcome(true,
            (hadError ? "Netzwerk zurückgesetzt (einzelne Schritte mit Hinweisen). " : "Netzwerk vollständig zurückgesetzt. ")
            + "Bitte den PC jetzt neu starten.");
    }
}
