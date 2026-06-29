using System.IO;
using DariaTech.PcDoctor.Infrastructure;
using DariaTech.PcDoctor.Models;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.Core.Clone;

/// <summary>Info über die gefundene Klon-Engine (ddrescue).</summary>
public sealed record CloneEngineInfo(bool Found, string Path);

/// <summary>
/// Orchestriert das 1:1-Klonen über die externe Engine <c>ddrescue.exe</c>
/// (im Ordner <c>tools</c> neben der App). ddrescue kopiert sektorweise,
/// überspringt/wiederholt defekte Sektoren und schreibt ein Mapfile/Protokoll.
///
/// Sicherheits-Prinzip: Diese Klasse führt selbst KEINE rohen Schreibzugriffe
/// auf Datenträger aus – das macht ausschließlich die bewährte Engine. Vor dem
/// Start wird die Sicherheitsprüfung erneut ausgewertet (Defense in Depth).
/// </summary>
public sealed class DiskCloneService
{
    // ddrescue: [Optionen] QUELLE ZIEL MAPFILE  — Reihenfolge ist sicherheitskritisch!
    private const string ArgsTemplate = "-f -d -r3 {source} {target} {mapfile}";

    private readonly ILogger<DiskCloneService> _log;

    public DiskCloneService(ILogger<DiskCloneService> log) => _log = log;

    public CloneEngineInfo FindEngine()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "tools", "ddrescue.exe");
        return new CloneEngineInfo(File.Exists(path), path);
    }

    public async Task<FixOutcome> CloneAsync(
        PhysicalDisk source, PhysicalDisk target,
        IProgress<string> progress, CancellationToken ct = default)
    {
        // 1) Sicherheitsprüfung erneut – nie ohne erneute Freigabe schreiben.
        var validation = DiskCloneValidator.Validate(source, target);
        if (!validation.CanClone)
        {
            var why = string.Join(" ", validation.Errors);
            _log.LogWarning("Klonen blockiert: {Why}", why);
            return new FixOutcome(false, "Sicherheitsprüfung fehlgeschlagen: " + why);
        }

        // 2) Engine vorhanden?
        var engine = FindEngine();
        if (!engine.Found)
            return new FixOutcome(false,
                $"ddrescue.exe nicht gefunden ({engine.Path}). Bitte die Engine in den tools-Ordner legen.");

        // 3) Befehl bauen und ausführen.
        var logDir = Path.Combine(AppContext.BaseDirectory, "DariaTech-Klonlogs");
        Directory.CreateDirectory(logDir);
        var mapfile = Path.Combine(logDir, $"clone_{DateTime.Now:yyyy-MM-dd_HHmmss}.log");

        var args = BuildArgs(ArgsTemplate, source.DevicePath, target.DevicePath, mapfile);
        _log.LogWarning("Starte Klonen: {Source} -> {Target}", source.DevicePath, target.DevicePath);
        progress.Report($"Quelle: {source.Display}");
        progress.Report($"Ziel:   {target.Display}");
        progress.Report($"Befehl: ddrescue {args}");

        var result = await ProcessRunner.RunAsync(engine.Path, args, progress, ct).ConfigureAwait(false);

        return result.ExitCode == 0
            ? new FixOutcome(true, $"Klonen abgeschlossen. Protokoll: {mapfile}")
            : new FixOutcome(false,
                $"ddrescue endete mit Code {result.ExitCode}. Bei defekter Quelle ggf. erneut starten " +
                $"(ddrescue setzt über das Mapfile fort). Protokoll: {mapfile}");
    }

    /// <summary>
    /// Baut die ddrescue-Argumente. QUELLE steht vor ZIEL – durch Tests abgesichert,
    /// da ein Vertauschen die Quelle zerstören würde.
    /// </summary>
    public static string BuildArgs(string template, string source, string target, string mapfile)
        => template
            .Replace("{source}", Quote(source))
            .Replace("{target}", Quote(target))
            .Replace("{mapfile}", Quote(mapfile));

    private static string Quote(string value)
        => value.Contains(' ') ? $"\"{value}\"" : value;
}
