using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Leert den DNS-Cache (<c>ipconfig /flushdns</c>). Unkritisch und ohne
/// Neustart – hilft bei Namensauflösungs-Problemen.
/// </summary>
public sealed class FlushDnsFix : IFixAction
{
    public string Title => "DNS-Cache leeren";
    public string Description =>
        "Leert den DNS-Auflösungscache (ipconfig /flushdns). Hilft, wenn " +
        "Webseiten trotz Internetverbindung nicht erreichbar sind.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => false;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync("ipconfig.exe", "/flushdns", progress, ct)
            .ConfigureAwait(false);
        return result.ExitCode == 0
            ? new FixOutcome(true, "DNS-Cache geleert.")
            : new FixOutcome(false, $"DNS-Cache konnte nicht geleert werden (Code {result.ExitCode}).");
    }
}
