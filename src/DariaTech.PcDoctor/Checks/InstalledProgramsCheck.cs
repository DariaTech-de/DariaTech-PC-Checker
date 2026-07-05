using DariaTech.PcDoctor.Core;
using Microsoft.Win32;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Inventarisiert installierte Programme über die Uninstall-Registry-Schlüssel
/// (HKLM 64/32-Bit + HKCU) und markiert bekannte veraltete/unsichere Software
/// und Adware/Toolbars (siehe <see cref="ProgramRiskCatalog"/>) als Warnung.
/// Rein lesend – es wird nichts deinstalliert.
/// </summary>
public sealed class InstalledProgramsCheck : ICheck
{
    public string Area => "Installierte Programme";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            var programs = Enumerate(ct);

            results.Add(new CheckResult(Area, "Anzahl",
                $"{programs.Count} Programme installiert", Severity.Info));

            var flagged = programs
                .Select(p => (Program: p, Risk: ProgramRiskCatalog.Evaluate(p.Name, p.Version)))
                .Where(x => x.Risk is not null)
                .ToList();

            if (flagged.Count == 0)
            {
                results.Add(new CheckResult(Area, "Bewertung",
                    "Keine bekannten veralteten oder unerwünschten Programme gefunden", Severity.Ok));
            }
            else
            {
                foreach (var (program, risk) in flagged)
                {
                    var label = string.IsNullOrWhiteSpace(program.Version)
                        ? program.Name
                        : $"{program.Name} ({program.Version})";
                    results.Add(new CheckResult(Area, label, "veraltet/unerwünscht", Severity.Warning,
                        Detail: risk!.Reason, Tip: risk.Tip, OpenTarget: "ms-settings:appsfeatures"));
                }
            }

            return results;
        }, ct);

    /// <summary>Liest die installierten Programme aus den Uninstall-Registry-Zweigen.</summary>
    private static List<(string Name, string? Version)> Enumerate(CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(string, string?)>();

        void Scan(RegistryKey root, string path)
        {
            using var key = root.OpenSubKey(path);
            if (key is null) return;

            foreach (var sub in key.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                using var app = key.OpenSubKey(sub);
                if (app is null) continue;

                if (app.GetValue("DisplayName") is not string name || string.IsNullOrWhiteSpace(name)) continue;
                if (app.GetValue("SystemComponent") is int sc && sc == 1) continue;   // Systemkomponente
                if (app.GetValue("ParentKeyName") is not null) continue;              // Update/Teilkomponente
                if (app.GetValue("ReleaseType") is string rt &&
                    (rt.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                     rt.Contains("Hotfix", StringComparison.OrdinalIgnoreCase))) continue;

                var version = app.GetValue("DisplayVersion") as string;
                if (seen.Add($"{name.Trim()}|{version}"))
                    list.Add((name.Trim(), version));
            }
        }

        void ScanSafe(RegistryKey root, string path)
        {
            try { Scan(root, path); }
            catch (OperationCanceledException) { throw; }
            catch { /* Zweig nicht lesbar – überspringen */ }
        }

        ScanSafe(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        ScanSafe(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        ScanSafe(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

        return list.OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
