namespace DariaTech.PcDoctor.Models;

/// <summary>
/// Herausgeber-/Firmendaten und Markenelemente von DariaTech. Zentral, damit
/// Bericht und App dieselben Werte nutzen.
/// </summary>
public static class CompanyInfo
{
    /// <summary>
    /// Produktname (Platzhalter laut Projektvorgabe – hier zentral änderbar).
    /// Wird in App-Titel, Bericht und Wiederherstellungspunkt verwendet.
    /// </summary>
    public const string Product = "PC-Doktor";

    /// <summary>Produktname mit Markenpräfix, z. B. für Fenstertitel und Bericht.</summary>
    public const string ProductFull = "DariaTech " + Product;

    public const string Name = "DariaTech IT-Systemhaus";
    public const string Street = "Josef-Schmid-Weg 23";
    public const string City = "87700 Memmingen";
    public const string Phone = "+49 8331 99 59 369";
    public const string Email = "kontakt@dariatech.de";

    // Markenfarben (aus dem DariaTech-Logo)
    public const string BrandDark = "#0E3B34";   // Petrol/Dunkelgrün
    public const string BrandGreen = "#2FA86A";  // Hauptgrün
    public const string BrandShadow = "#1C6E46"; // dunklerer Grünton
    public const string BrandMint = "#6FE0A8";   // helles Mint (Akzent)

    /// <summary>
    /// Inline-SVG der DariaTech-Diamant-Marke (facettiert) in der Kantenlänge
    /// <paramref name="size"/> Pixel – für den HTML-Bericht (scharf/skalierbar).
    /// </summary>
    public static string LogoSvg(int size = 40) =>
        $"""
        <svg width="{size}" height="{size}" viewBox="0 0 64 64" xmlns="http://www.w3.org/2000/svg" aria-label="DariaTech">
          <polygon points="32,8 32,33 32,56 10.5,32" fill="{BrandGreen}"/>
          <polygon points="32,8 53.5,32 32,56 32,33" fill="{BrandShadow}"/>
          <polygon points="32,8 42.75,20 32,33 21.25,20" fill="{BrandMint}"/>
        </svg>
        """;
}
