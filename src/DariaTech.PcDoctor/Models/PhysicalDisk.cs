namespace DariaTech.PcDoctor.Models;

/// <summary>
/// Ein physischer Datenträger (für den Klon-Assistenten). Über eine
/// Dockingstation angeschlossene Platten erscheinen i. d. R. als USB-Bus.
///
/// <para><b>Wichtig zum Schutzkennzeichen:</b> Ob eine Platte die System-/
/// Startplatte ist, entscheidet darüber, ob sie als Klon-ZIEL überschrieben
/// werden darf. Deshalb wird dieser Zustand aus <b>zwei unabhängigen Quellen</b>
/// beurteilt (WMI-Kennzeichen und Lage des laufenden Windows) und ein
/// <b>unbekannter</b> Zustand gilt als geschützt. Lieber ein Klonvorgang, der
/// sich verweigert, als eine überschriebene Kundenplatte.</para>
/// </summary>
/// <param name="IsSystem">WMI-Kennzeichen „Systemplatte“ (false, wenn unbekannt).</param>
/// <param name="IsBoot">WMI-Kennzeichen „Startplatte“ (false, wenn unbekannt).</param>
/// <param name="ProtectionUnknown">
/// True, wenn sich der Schutzstatus nicht ermitteln ließ – wird dann wie
/// „geschützt“ behandelt.
/// </param>
/// <param name="HoldsWindows">
/// True, wenn auf dieser Platte das laufende Windows liegt – unabhängig von den
/// WMI-Kennzeichen ermittelt.
/// </param>
public sealed record PhysicalDisk(
    int Number,
    string Name,
    string Serial,
    long SizeBytes,
    string Bus,
    bool IsSystem,
    bool IsBoot,
    string Health,
    bool ProtectionUnknown = false,
    bool HoldsWindows = false)
{
    /// <summary>Windows-Gerätepfad, z. B. <c>\\.\PhysicalDrive2</c>.</summary>
    public string DevicePath => $@"\\.\PhysicalDrive{Number}";

    /// <summary>
    /// True, wenn diese Platte nicht als Klon-Ziel dienen darf. Ein unbekannter
    /// Zustand zählt bewusst als geschützt (fail-safe).
    /// </summary>
    public bool IsProtected => IsSystem || IsBoot || HoldsWindows || ProtectionUnknown;

    /// <summary>Grund des Schutzes im Klartext – oder <c>null</c>, wenn nicht geschützt.</summary>
    public string? ProtectionReason
    {
        get
        {
            if (IsSystem || IsBoot) return "ist die System-/Startplatte";
            if (HoldsWindows) return "enthält das laufende Windows";
            if (ProtectionUnknown)
                return "Schutzstatus nicht ermittelbar – aus Sicherheitsgründen gesperrt";
            return null;
        }
    }

    public string SizeText => SizeBytes >= 1L << 40
        ? $"{SizeBytes / (double)(1L << 40):0.0} TB"
        : $"{SizeBytes / (double)(1L << 30):0.0} GB";

    public string Display =>
        $"Disk {Number} · {Name} · {SizeText} · {Bus}" +
        (IsProtected ? " · ⚠ GESCHÜTZT" : string.Empty) +
        $" · SMART {Health}";
}
