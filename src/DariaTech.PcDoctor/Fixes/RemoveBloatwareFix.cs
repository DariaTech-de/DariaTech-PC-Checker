using System.Text.RegularExpressions;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Entfernt eine kuratierte Liste eindeutig werblicher Vorinstallations-Apps
/// (siehe <see cref="BloatwareCatalog"/>) – nur für den aktuellen Benutzer und
/// nur, wenn vorhanden. Reversibel: alle Apps sind aus dem Microsoft Store
/// jederzeit wieder installierbar. Kein Wiederherstellungspunkt nötig (keine
/// System-, sondern reine Benutzer-App-Änderung), aber Bestätigung über die UI.
/// </summary>
public sealed class RemoveBloatwareFix : IFixAction
{
    public string Title => "Werbe-/Bloatware-Apps entfernen";

    public string Description =>
        "Entfernt gängige vorinstallierte Werbe-Apps für den aktuellen Benutzer – " +
        "Spiele-Stubs (Candy Crush u. Ä.), 3D-Builder/-Viewer, Mixed-Reality-Portal, die " +
        "„Tipps“-App, werbefinanziertes Solitär und das eingestellte Consumer-Skype. " +
        "Es werden ausschließlich diese bekannten Apps angefasst – keine System-, Office- oder " +
        "Sicherheitskomponenten und keine persönlichen Daten. Alle entfernten Apps lassen sich " +
        "jederzeit kostenlos aus dem Microsoft Store neu installieren (daher umkehrbar). " +
        "Nicht vorhandene Apps werden einfach übersprungen.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        // Nur Pakete aus dem Katalog, die nicht auf der Schutzliste stehen.
        var targets = BloatwareCatalog.Packages
            .Where(p => !BloatwareCatalog.NeverRemove.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (targets.Count == 0)
            return new FixOutcome(true, "Keine zu entfernenden Apps definiert.");

        var namesLiteral = string.Join(",", targets.Select(p => $"'{p}'"));
        var script =
            $"$names=@({namesLiteral}); foreach($n in $names){{" +
            "$p=Get-AppxPackage -Name $n; " +
            "if($p){try{$p|Remove-AppxPackage -ErrorAction Stop; Write-Output ('OK '+$n)}" +
            "catch{Write-Output ('FAIL '+$n)}}else{Write-Output ('SKIP '+$n)}}";

        progress.Report("Entferne Werbe-Apps (nur aktueller Benutzer) …");
        var result = await ProcessRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
            progress, ct).ConfigureAwait(false);

        var removed = Regex.Matches(result.Output, @"(?m)^OK\s").Count;
        var failed = Regex.Matches(result.Output, @"(?m)^FAIL\s").Count;

        var msg = $"{removed} Werbe-App(s) entfernt" +
                  (failed > 0 ? $", {failed} nicht entfernbar" : string.Empty) +
                  ". Nicht vorhandene wurden übersprungen. Bei Bedarf im Microsoft Store neu installierbar.";
        progress.Report(msg);
        return new FixOutcome(true, msg);
    }
}
