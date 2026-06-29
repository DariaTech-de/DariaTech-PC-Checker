using DariaTech.PcDoctor.Core.Clone;
using DariaTech.PcDoctor.Models;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class DiskCloneTests
{
    private const long Gb = 1L << 30;

    private static PhysicalDisk Disk(int number, long sizeGb, bool system = false,
        string health = "Healthy", string bus = "USB")
        => new(number, $"Disk{number}", $"SN{number}", sizeGb * Gb, bus, system, false, health);

    [Fact]
    public void Validate_TargetIsSystemDisk_Blocked()
    {
        var v = DiskCloneValidator.Validate(Disk(1, 500), Disk(0, 1000, system: true));
        Assert.False(v.CanClone);
        Assert.Contains(v.Errors, e => e.Contains("System", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TargetSmallerThanSource_Blocked()
    {
        var v = DiskCloneValidator.Validate(Disk(1, 1000), Disk(2, 500));
        Assert.False(v.CanClone);
        Assert.Contains(v.Errors, e => e.Contains("kleiner"));
    }

    [Fact]
    public void Validate_SameDisk_Blocked()
    {
        var v = DiskCloneValidator.Validate(Disk(2, 500), Disk(2, 500));
        Assert.False(v.CanClone);
        Assert.Contains(v.Errors, e => e.Contains("dasselbe"));
    }

    [Fact]
    public void Validate_HealthyValidPair_CanCloneWithEraseWarning()
    {
        var v = DiskCloneValidator.Validate(Disk(1, 500), Disk(2, 1000));
        Assert.True(v.CanClone);
        Assert.Contains(v.Warnings, w => w.Contains("überschrieben"));
    }

    [Fact]
    public void Validate_UnhealthySource_WarnsButAllows()
    {
        var v = DiskCloneValidator.Validate(Disk(1, 500, health: "Unhealthy"), Disk(2, 500));
        Assert.True(v.CanClone);
        Assert.Contains(v.Warnings, w => w.Contains("ddrescue"));
    }

    [Fact]
    public void BuildArgs_PutsSourceBeforeTarget()
    {
        var args = DiskCloneService.BuildArgs("-f {source} {target} {mapfile}",
            @"\\.\PhysicalDrive2", @"\\.\PhysicalDrive3", @"C:\logs\map.log");

        var iSource = args.IndexOf(@"\\.\PhysicalDrive2", System.StringComparison.Ordinal);
        var iTarget = args.IndexOf(@"\\.\PhysicalDrive3", System.StringComparison.Ordinal);
        Assert.True(iSource >= 0 && iTarget > iSource, "Quelle muss vor Ziel stehen");
    }

    [Fact]
    public void BuildArgs_QuotesPathsContainingSpaces()
    {
        var args = DiskCloneService.BuildArgs("{mapfile}", "a", "b", @"C:\Pfad mit Leer\map.log");
        Assert.Contains("\"C:\\Pfad mit Leer\\map.log\"", args);
    }
}
