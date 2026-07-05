using System.Reflection;

namespace DariaTech.PcDoctor.Models;

/// <summary>
/// Laufzeit-Infos zur Anwendung (Version). Wichtig fürs USB-Stick-Szenario:
/// so ist auf einen Blick erkennbar, welcher Build gerade läuft.
/// </summary>
public static class AppInfo
{
    /// <summary>Anzeigeversion, z. B. „0.1.0". Fällt bei Fehlern auf „0.0.0" zurück.</summary>
    public static string Version { get; } = Resolve();

    /// <summary>Produktname mit Version, z. B. „DariaTech PC-Doktor 0.1.0".</summary>
    public static string ProductWithVersion => $"{CompanyInfo.ProductFull} {Version}";

    private static string Resolve()
    {
        try
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            // Bevorzugt die InformationalVersion (kann Suffixe wie "-beta" enthalten).
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');   // Build-Metadaten abschneiden
                return plus > 0 ? info[..plus] : info;
            }

            return asm.GetName().Version?.ToString(3) ?? "0.0.0";
        }
        catch
        {
            return "0.0.0";
        }
    }
}
