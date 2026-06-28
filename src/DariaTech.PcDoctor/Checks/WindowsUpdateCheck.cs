using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Windows-Updates: sucht über die COM-API <c>Microsoft.Update.Session</c> nach
/// ausstehenden Software-Updates (<c>IsInstalled=0 and Type='Software'</c>).
/// Diese Suche ist langsam und läuft daher als abbrechbarer Task; über
/// <see cref="ScanOptions.SkipWindowsUpdate"/> ist sie überspringbar.
/// Ausstehende Updates → Warnung.
/// </summary>
public sealed class WindowsUpdateCheck : ICheck
{
    private readonly ScanOptions _options;

    public WindowsUpdateCheck(ScanOptions options) => _options = options;

    public string Area => "Windows-Updates";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
    {
        if (_options.SkipWindowsUpdate)
            return Task.FromResult<IReadOnlyList<CheckResult>>(new[]
            {
                new CheckResult(Area, "Prüfung", "übersprungen (Schnellmodus)", Severity.Info)
            });

        return Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("Update-API nicht verfügbar.");
                dynamic session = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();

                ct.ThrowIfCancellationRequested();
                dynamic result = searcher.Search("IsInstalled=0 and Type='Software'");
                int count = result.Updates.Count;

                return count > 0
                    ? new[]
                    {
                        new CheckResult(Area, "Ausstehend", $"{count} Update(s)", Severity.Warning,
                            $"{count} Windows-Update(s) ausstehend – installieren.")
                    }
                    : new[]
                    {
                        new CheckResult(Area, "Status", "auf dem neuesten Stand", Severity.Ok)
                    };
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                return new[]
                {
                    new CheckResult(Area, "Hinweis",
                        "Update-Prüfung nicht möglich (offline?)", Severity.Info)
                };
            }
        }, ct);
    }
}
