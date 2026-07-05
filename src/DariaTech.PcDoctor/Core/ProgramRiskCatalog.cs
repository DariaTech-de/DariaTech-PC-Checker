namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Bewertet installierte Programme anhand einer kuratierten Liste bekannter
/// veralteter/unsicherer Software (End-of-Life) sowie typischer Adware/Toolbars.
/// Rein funktional und damit gut testbar. Bewusst konservativ: im Zweifel wird
/// nichts markiert, um Fehlalarme zu vermeiden.
/// </summary>
public static class ProgramRiskCatalog
{
    /// <summary>Ein Fund: warum das Programm auffällt und wie man es entfernt.</summary>
    public sealed record Risk(string Reason, string Tip);

    private const string UninstallTip =
        "So entfernen: Einstellungen → „Apps“ → „Installierte Apps“ (bzw. Systemsteuerung → " +
        "„Programme und Features“), den Eintrag suchen und deinstallieren.";

    /// <summary>End-of-Life-/unsichere Software – Name (klein) → Begründung.</summary>
    private static readonly (string Needle, string Reason)[] EndOfLife =
    {
        ("flash player", "Adobe Flash Player wurde Ende 2020 eingestellt und erhält keine Sicherheitsupdates mehr."),
        ("shockwave", "Adobe Shockwave Player ist eingestellt (End-of-Life) und gilt als unsicher."),
        ("silverlight", "Microsoft Silverlight wird nicht mehr unterstützt und von keinem aktuellen Browser genutzt."),
        ("quicktime", "Apple QuickTime für Windows erhält seit 2016 keine Sicherheitsupdates mehr."),
    };

    /// <summary>Bekannte Adware/Toolbars/PUPs (Name klein).</summary>
    private static readonly string[] UnwantedNeedles =
    {
        "ask toolbar", "mywebsearch", "my web search", "babylon", "conduit",
        "delta toolbar", "delta search", "searchprotect", "search protect",
        "webcompanion", "wajam",
    };

    private const string UnwantedReason =
        "Gilt als unerwünschtes Programm (Toolbar/Adware) – bremst den Browser, blendet Werbung " +
        "ein und ändert oft Suchmaschine/Startseite.";

    /// <summary>
    /// Bewertet ein Programm. Liefert <c>null</c>, wenn nichts bekannt/auffällig ist.
    /// </summary>
    public static Risk? Evaluate(string? displayName, string? version = null)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return null;
        var n = displayName.ToLowerInvariant();

        foreach (var (needle, reason) in EndOfLife)
            if (n.Contains(needle))
                return new Risk(reason, UninstallTip);

        // Alte Java-Laufzeiten (Java 6/7 sind End-of-Life und ein häufiges Einfallstor).
        if (n.Contains("java") && (n.Contains(" 6 update") || n.Contains(" 7 update") ||
                                    n.Contains("(tm) 6") || n.Contains("(tm) 7") ||
                                    n.Contains("j2se")))
            return new Risk("Diese Java-Version (6/7) ist End-of-Life und ein bekanntes Sicherheitsrisiko. " +
                            "Nur behalten, wenn eine Altanwendung sie zwingend benötigt.", UninstallTip);

        foreach (var needle in UnwantedNeedles)
            if (n.Contains(needle))
                return new Risk(UnwantedReason, UninstallTip);

        return null;
    }
}
