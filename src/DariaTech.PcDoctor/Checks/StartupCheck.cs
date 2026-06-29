using System.Management;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Fixes;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Autostart-Programme (<c>Win32_StartupCommand</c>): Anzahl + erste Einträge.
/// Mehr als 15 Einträge → Hinweis (Warnung), da viele den Start bremsen.
/// Jeder gelistete Eintrag erhält eine reversible „Deaktivieren“-Aktion.
/// </summary>
public sealed class StartupCheck : ICheck
{
    public string Area => "Autostart-Programme";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            var entries = new List<(string Name, string Location, string Command)>();

            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name, Location, Command FROM Win32_StartupCommand"))
            {
                foreach (ManagementBaseObject obj in searcher.Get())
                {
                    ct.ThrowIfCancellationRequested();
                    entries.Add(($"{obj["Name"]}", $"{obj["Location"]}", $"{obj["Command"]}"));
                }
            }

            var count = entries.Count;
            results.Add(new CheckResult(Area, "Anzahl",
                $"{count} Einträge",
                count > 15 ? Severity.Warning : Severity.Info,
                count > 15 ? $"{count} Autostart-Einträge – viele davon bremsen den Start." : null,
                Tip: count > 15
                    ? "So beheben: Nicht benötigte Programme aus dem Autostart nehmen – entweder hier " +
                      "in der App unten je Eintrag auf „Deaktivieren“ (reversibel), oder in Einstellungen → " +
                      "Apps → Autostart bzw. im Task-Manager (Strg+Umschalt+Esc) unter „Autostart-Apps“."
                    : null,
                OpenTarget: count > 15 ? "ms-settings:startupapps" : null));

            foreach (var (name, location, command) in entries.Take(8))
            {
                var fix = new DisableStartupItemFix(name, location, command);
                results.Add(new CheckResult(Area, $" – {name}", location, Severity.Info,
                    Detail: command, Fixes: new IFixAction[] { fix }));
            }

            return results;
        }, ct);
}
