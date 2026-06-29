using System.Management;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Models;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>
/// Liest die physischen Datenträger über <c>MSFT_Disk</c>
/// (root\Microsoft\Windows\Storage) – inkl. System-/Start-Kennzeichnung,
/// Bus-Typ (USB = Dockingstation) und SMART-Gesundheit.
/// </summary>
public sealed class WmiPhysicalDiskService : IPhysicalDiskService
{
    private readonly ILogger<WmiPhysicalDiskService> _log;

    public WmiPhysicalDiskService(ILogger<WmiPhysicalDiskService> log) => _log = log;

    public IReadOnlyList<PhysicalDisk> Enumerate()
    {
        var disks = new List<PhysicalDisk>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            var query = new ObjectQuery(
                "SELECT Number, FriendlyName, SerialNumber, Size, BusType, IsSystem, IsBoot, HealthStatus FROM MSFT_Disk");
            using var searcher = new ManagementObjectSearcher(scope, query);

            foreach (ManagementBaseObject d in searcher.Get())
            {
                if (d["Number"] is null) continue;
                disks.Add(new PhysicalDisk(
                    Number: Convert.ToInt32(d["Number"]),
                    Name: $"{d["FriendlyName"]}".Trim(),
                    Serial: $"{d["SerialNumber"]}".Trim(),
                    SizeBytes: Convert.ToInt64(d["Size"] ?? 0L),
                    Bus: BusText(d["BusType"]),
                    IsSystem: d["IsSystem"] is bool s && s,
                    IsBoot: d["IsBoot"] is bool b && b,
                    Health: HealthText(d["HealthStatus"])));
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Physische Datenträger konnten nicht gelesen werden");
        }

        return disks.OrderBy(d => d.Number).ToList();
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
