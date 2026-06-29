using System.Diagnostics;

namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Öffnet die passende Windows-Stelle zu einem Befund („Hier öffnen"-Button im
/// Detail-Popup). Versteht einfache Sprungziele:
/// <list type="bullet">
///   <item>Einstellungs-URIs, z. B. <c>ms-settings:windowsupdate</c>, <c>windowsdefender:</c></item>
///   <item>MMC-Snap-Ins, z. B. <c>devmgmt.msc</c>, <c>eventvwr.msc</c></item>
///   <item>Systemprogramme/Systemsteuerung, z. B. <c>cleanmgr</c>, <c>taskmgr</c>, <c>appwiz.cpl</c></item>
///   <item>Shell-Ordner, z. B. <c>shell:startup</c></item>
/// </list>
/// Bewusst nur Öffnen/Anzeigen – nichts wird verändert.
/// </summary>
public static class SystemLauncher
{
    /// <summary>
    /// Öffnet das angegebene Ziel über die Windows-Shell. Liefert <c>true</c>,
    /// wenn der Start ausgelöst werden konnte.
    /// </summary>
    public static bool Open(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
