namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Übersetzt die WMI-Rohwerte von <c>MSFT_PhysicalDisk</c> (MediaType,
/// SpindleSpeed) in einen lesbaren Datenträgertyp. Rein funktional, gut testbar.
/// </summary>
public static class DiskMediaClassifier
{
    public enum Media { Unknown, Hdd, Ssd }

    /// <summary>
    /// Ermittelt den Typ. <paramref name="mediaType"/> ist der WMI-Wert
    /// (3 = HDD, 4 = SSD, 5 = SCM). Ist er unspezifisch (0), wird die
    /// Umdrehungszahl herangezogen (0 → SSD, &gt; 0 → HDD).
    /// </summary>
    public static Media Classify(int mediaType, uint? spindleSpeed)
    {
        switch (mediaType)
        {
            case 3: return Media.Hdd;
            case 4: return Media.Ssd;
            case 5: return Media.Ssd; // Storage Class Memory – wie SSD behandeln
        }

        if (spindleSpeed is uint s)
            return s == 0 ? Media.Ssd : Media.Hdd;

        return Media.Unknown;
    }

    public static string Label(Media media) => media switch
    {
        Media.Ssd => "SSD",
        Media.Hdd => "HDD (klassische Festplatte)",
        _ => "unbekannt"
    };
}
