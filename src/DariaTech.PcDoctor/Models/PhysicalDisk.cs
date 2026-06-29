namespace DariaTech.PcDoctor.Models;

/// <summary>
/// Ein physischer Datenträger (für den Klon-Assistenten). Über eine
/// Dockingstation angeschlossene Platten erscheinen i. d. R. als USB-Bus.
/// </summary>
public sealed record PhysicalDisk(
    int Number,
    string Name,
    string Serial,
    long SizeBytes,
    string Bus,
    bool IsSystem,
    bool IsBoot,
    string Health)
{
    /// <summary>Windows-Gerätepfad, z. B. <c>\\.\PhysicalDrive2</c>.</summary>
    public string DevicePath => $@"\\.\PhysicalDrive{Number}";

    public bool IsProtected => IsSystem || IsBoot;

    public string SizeText => SizeBytes >= 1L << 40
        ? $"{SizeBytes / (double)(1L << 40):0.0} TB"
        : $"{SizeBytes / (double)(1L << 30):0.0} GB";

    public string Display =>
        $"Disk {Number} · {Name} · {SizeText} · {Bus}" +
        (IsProtected ? " · ⚠ SYSTEM" : string.Empty) +
        $" · SMART {Health}";
}
