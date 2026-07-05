namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Eng begrenzte, kuratierte Liste eindeutig werblicher/entbehrlicher
/// Vorinstallations-Apps (UWP), die gefahrlos für den aktuellen Benutzer
/// entfernt werden können – alle aus dem Microsoft Store jederzeit wieder
/// installierbar. Bewusst konservativ: nur bekannte Werbe-/Spiele-Stubs und
/// klar entbehrliche Microsoft-Apps. KEINE system- oder sicherheitsrelevanten
/// Pakete. Rein deklarativ (testbar).
/// </summary>
public static class BloatwareCatalog
{
    /// <summary>Exakte AppX-Paketnamen (Family-Name-Präfix), die entfernt werden dürfen.</summary>
    public static readonly IReadOnlyList<string> Packages = new[]
    {
        // Werbe-Spiele (Drittanbieter)
        "king.com.CandyCrushSaga",
        "king.com.CandyCrushSodaSaga",
        "king.com.BubbleWitch3Saga",
        "king.com.FarmHeroesSaga",
        // Entbehrliche Microsoft-Beigaben
        "Microsoft.3DBuilder",
        "Microsoft.Microsoft3DViewer",
        "Microsoft.MixedReality.Portal",
        "Microsoft.Getstarted",                    // „Tipps"
        "Microsoft.MicrosoftSolitaireCollection",  // werbefinanziertes Solitär
        "Microsoft.SkypeApp",                       // Consumer-Skype (eingestellt)
    };

    /// <summary>
    /// System-/sicherheitsrelevante Pakete, die NIE entfernt werden dürfen –
    /// als Schutznetz gegen versehentliche Katalog-Erweiterungen.
    /// </summary>
    public static readonly IReadOnlyList<string> NeverRemove = new[]
    {
        "Microsoft.WindowsStore",
        "Microsoft.SecHealthUI",
        "Microsoft.Windows.Photos",
        "Microsoft.WindowsCalculator",
        "Microsoft.DesktopAppInstaller",
        "Microsoft.WindowsTerminal",
    };
}
