using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Aktualisiert die Gruppenrichtlinien (<c>gpupdate /force</c>). Nützlich auf
/// Domänen-PCs, wenn Richtlinien/Laufwerke/Drucker nicht ankommen.
/// </summary>
public sealed class GroupPolicyUpdateFix : IFixAction
{
    public string Title => "Gruppenrichtlinien aktualisieren (gpupdate)";
    public string Description =>
        "Erzwingt die Aktualisierung der Gruppenrichtlinien (gpupdate /force). " +
        "Hilft auf Domänen-PCs, wenn Richtlinien, Laufwerke oder Drucker fehlen.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Aktualisiere Gruppenrichtlinien …");
        var result = await ProcessRunner.RunAsync("gpupdate.exe", "/force", progress, ct).ConfigureAwait(false);

        return result.ExitCode == 0
            ? new FixOutcome(true, "Gruppenrichtlinien aktualisiert.")
            : new FixOutcome(false, $"gpupdate endete mit Code {result.ExitCode} (kein Domänen-PC?).");
    }
}
