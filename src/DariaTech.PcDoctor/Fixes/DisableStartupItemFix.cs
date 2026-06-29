using System.IO;
using DariaTech.PcDoctor.Core;
using Microsoft.Win32;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Deaktiviert einen einzelnen Autostart-Eintrag <b>reversibel</b> (deaktivieren
/// statt löschen):
/// <list type="bullet">
///   <item>Registry-Run-Werte werden in einen nicht ausgeführten Backup-Unterschlüssel
///   (<c>…\Run\DariaTech_Deaktiviert</c>) verschoben. Windows führt nur Werte direkt
///   unter dem Run-Schlüssel aus, keine Unterschlüssel – der Eintrag startet also
///   nicht mehr, bleibt aber erhalten.</item>
///   <item>Verknüpfungen im Autostart-Ordner werden in den Unterordner
///   „Deaktiviert (DariaTech)“ verschoben.</item>
/// </list>
/// Diese Instanz wird vom <c>StartupCheck</c> je Eintrag erzeugt (nicht über DI).
/// </summary>
public sealed class DisableStartupItemFix : IFixAction
{
    private const string RegistryBackupKey = "DariaTech_Deaktiviert";
    private const string FolderBackupName = "Deaktiviert (DariaTech)";

    private readonly string _name;
    private readonly string _location;
    private readonly string _command;

    public DisableStartupItemFix(string name, string location, string command)
    {
        _name = name;
        _location = location;
        _command = command;
    }

    public string Title => $"Autostart deaktivieren: {_name}";
    public string Description =>
        $"Deaktiviert den Autostart-Eintrag „{_name}“ reversibel (er wird nicht " +
        "gelöscht, sondern in einen Backup-Ort verschoben und kann später wieder " +
        "aktiviert werden).";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;

    public Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
        => Task.Run(() =>
        {
            try
            {
                if (TryParseRegistry(_location, out var hive, out var subPath))
                    return DisableRegistryValue(hive, subPath, progress);

                return DisableStartupFolderEntry(progress);
            }
            catch (Exception ex)
            {
                return new FixOutcome(false, $"Konnte „{_name}“ nicht deaktivieren: {ex.Message}");
            }
        }, ct);

    private FixOutcome DisableRegistryValue(RegistryKey hive, string subPath, IProgress<string> progress)
    {
        using var runKey = hive.OpenSubKey(subPath, writable: true);
        if (runKey is null)
            return new FixOutcome(false, $"Registry-Schlüssel nicht gefunden: {_location}");

        var value = runKey.GetValue(_name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null)
            return new FixOutcome(true, $"„{_name}“ war bereits nicht (mehr) aktiv.");

        var kind = runKey.GetValueKind(_name);
        progress.Report($"Sichere Registry-Wert „{_name}“ und entferne ihn aus dem Autostart …");

        using (var backup = runKey.CreateSubKey(RegistryBackupKey, writable: true))
        {
            backup!.SetValue(_name, value, kind);
        }
        runKey.DeleteValue(_name);

        return new FixOutcome(true,
            $"Autostart „{_name}“ deaktiviert (gesichert unter {RegistryBackupKey}).");
    }

    private FixOutcome DisableStartupFolderEntry(IProgress<string> progress)
    {
        var folder = ResolveStartupFolder();
        if (folder is null || !Directory.Exists(folder))
            return new FixOutcome(false,
                $"Autostart-Ablage für „{_name}“ nicht gefunden ({_location}).");

        var link = FindLink(folder);
        if (link is null)
            return new FixOutcome(true, $"„{_name}“ war bereits nicht (mehr) im Autostart-Ordner.");

        var backupDir = Path.Combine(folder, FolderBackupName);
        Directory.CreateDirectory(backupDir);
        var target = Path.Combine(backupDir, Path.GetFileName(link));

        progress.Report($"Verschiebe „{Path.GetFileName(link)}“ nach „{FolderBackupName}“ …");
        if (File.Exists(target)) File.Delete(target);
        File.Move(link, target);

        return new FixOutcome(true, $"Autostart „{_name}“ deaktiviert (in „{FolderBackupName}“ verschoben).");
    }

    private string? ResolveStartupFolder()
    {
        // Location ist entweder ein Pfad oder ein bekannter Bezeichner.
        if (_location.Contains('\\') && Directory.Exists(_location))
            return _location;

        return _location.ToLowerInvariant() switch
        {
            "common startup" => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            "startup" => Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            _ => Environment.GetFolderPath(Environment.SpecialFolder.Startup)
        };
    }

    private string? FindLink(string folder)
    {
        // 1) Über den Namen (häufigster Fall), 2) über die Command-Zeile.
        foreach (var candidate in new[]
                 {
                     Path.Combine(folder, _name),
                     Path.Combine(folder, _name + ".lnk")
                 })
        {
            if (File.Exists(candidate)) return candidate;
        }

        try
        {
            var match = Directory.EnumerateFiles(folder)
                .FirstOrDefault(f => string.Equals(
                    Path.GetFileNameWithoutExtension(f), _name,
                    StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        catch { /* ignoriert */ }

        return null;
    }

    private static bool TryParseRegistry(string location, out RegistryKey hive, out string subPath)
    {
        hive = Registry.CurrentUser;
        subPath = string.Empty;
        if (string.IsNullOrWhiteSpace(location)) return false;

        var loc = location.Replace('/', '\\').Trim();
        var firstSep = loc.IndexOf('\\');
        if (firstSep < 0) return false;

        var root = loc[..firstSep].ToUpperInvariant();
        var rest = loc[(firstSep + 1)..];

        switch (root)
        {
            case "HKLM":
            case "HKEY_LOCAL_MACHINE":
                hive = Registry.LocalMachine;
                subPath = rest;
                return true;
            case "HKCU":
            case "HKEY_CURRENT_USER":
                hive = Registry.CurrentUser;
                subPath = rest;
                return true;
            case "HKU":
            case "HKEY_USERS":
                hive = Registry.Users;
                subPath = rest; // enthält die SID als ersten Teilpfad
                return true;
            default:
                return false;
        }
    }
}
