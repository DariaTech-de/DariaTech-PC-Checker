using System.IO;
using System.Management;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Models;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>
/// Liest die physischen Datenträger über <c>MSFT_Disk</c>
/// (root\Microsoft\Windows\Storage) – inkl. System-/Start-Kennzeichnung,
/// Bus-Typ (USB = Dockingstation) und SMART-Gesundheit.
///
/// <para><b>Sicherheitsregel:</b> Das Schutzkennzeichen entscheidet, ob eine
/// Platte als Klon-Ziel überschrieben werden darf. Fehlt es (WMI liefert für
/// <c>IsSystem</c>/<c>IsBoot</c> keinen Wert), wird die Platte als geschützt
/// gemeldet – nicht als frei. Zusätzlich wird unabhängig davon ermittelt, auf
/// welcher Platte das laufende Windows liegt.</para>
/// </summary>
public sealed class WmiPhysicalDiskService : IPhysicalDiskService
{
    private readonly ILogger<WmiPhysicalDiskService> _log;

    public WmiPhysicalDiskService(ILogger<WmiPhysicalDiskService> log) => _log = log;

    public IReadOnlyList<PhysicalDisk> Enumerate()
    {
        var disks = new List<PhysicalDisk>();

        // Unabhängige zweite Quelle: Auf welcher Platte liegt das laufende Windows?
        var windowsDisk = ReadWindowsDiskNumber();

        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            var query = new ObjectQuery(
                "SELECT Number, FriendlyName, SerialNumber, Size, BusType, IsSystem, IsBoot, HealthStatus FROM MSFT_Disk");
            using var searcher = new ManagementObjectSearcher(scope, query);

            foreach (ManagementBaseObject d in searcher.Get())
            {
                if (d["Number"] is null) continue;
                var number = Convert.ToInt32(d["Number"]);

                // Fehlende Kennzeichen NICHT als „false“ durchwinken.
                var isSystem = d["IsSystem"] as bool?;
                var isBoot = d["IsBoot"] as bool?;
                var unknown = isSystem is null && isBoot is null;

                if (unknown)
                    _log.LogWarning(
                        "Disk {Number}: Schutzstatus (IsSystem/IsBoot) nicht lesbar – " +
                        "wird als geschützt behandelt.", number);

                disks.Add(new PhysicalDisk(
                    Number: number,
                    Name: $"{d["FriendlyName"]}".Trim(),
                    Serial: $"{d["SerialNumber"]}".Trim(),
                    SizeBytes: Convert.ToInt64(d["Size"] ?? 0L),
                    Bus: BusText(d["BusType"]),
                    IsSystem: isSystem ?? false,
                    IsBoot: isBoot ?? false,
                    Health: HealthText(d["HealthStatus"]),
                    ProtectionUnknown: unknown,
                    HoldsWindows: windowsDisk is int win && win == number));
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Physische Datenträger konnten nicht gelesen werden");
        }

        return disks.OrderBy(d => d.Number).ToList();
    }

    /// <summary>
    /// Nummer der Platte, auf der das laufende Windows liegt – oder <c>null</c>,
    /// wenn sie sich nicht ermitteln lässt. Bewusst über eine zweite WMI-Klasse
    /// (<c>MSFT_Partition</c>), damit ein Ausfall der ersten Quelle nicht dazu
    /// führt, dass die Systemplatte plötzlich als freies Ziel gilt.
    /// </summary>
    private int? ReadWindowsDiskNumber()
    {
        var letter = WindowsDriveLetter(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (letter is null) return null;

        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT DiskNumber, DriveLetter FROM MSFT_Partition"));

            foreach (ManagementBaseObject item in searcher.Get())
            {
                using var partition = (ManagementObject)item;

                // DriveLetter kommt als char (bzw. 0, wenn keiner zugewiesen ist).
                var raw = partition["DriveLetter"];
                if (raw is null) continue;
                var text = raw.ToString();
                if (string.IsNullOrEmpty(text)) continue;

                if (char.ToUpperInvariant(text[0]) != letter.Value) continue;
                if (partition["DiskNumber"] is null) continue;

                return Convert.ToInt32(partition["DiskNumber"]);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Windows-Laufwerk konnte keiner Platte zugeordnet werden");
        }

        return null;
    }

    /// <summary>
    /// Laufwerksbuchstabe des Windows-Ordners in Großschreibung – z. B.
    /// <c>C:\Windows</c> → <c>'C'</c>. Rein funktional (testbar).
    /// </summary>
    public static char? WindowsDriveLetter(string? windowsPath)
    {
        var root = string.IsNullOrWhiteSpace(windowsPath) ? null : Path.GetPathRoot(windowsPath);
        if (string.IsNullOrEmpty(root)) return null;

        var first = char.ToUpperInvariant(root[0]);
        return first is >= 'A' and <= 'Z' ? first : null;
    }

    private static string HealthText(object? value)
    {
        if (value is null) return "Unbekannt";
        return Convert.ToInt32(value) switch
        {
            0 => "Healthy",
            1 => "Warning",
            2 => "Unhealthy",
            _ => "Unbekannt"
        };
    }

    private static string BusText(object? value)
    {
        if (value is null) return "?";
        return Convert.ToInt32(value) switch
        {
            1 => "SCSI",
            3 => "ATA",
            7 => "USB",
            8 => "RAID",
            10 => "SAS",
            11 => "SATA",
            12 => "SD",
            13 => "MMC",
            17 => "NVMe",
            _ => "sonstige"
        };
    }
}
