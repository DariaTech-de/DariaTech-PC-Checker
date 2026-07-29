namespace DariaTech.PcDoctor.Core;

/// <summary>Ein einzelner Anwendungsabsturz aus dem Ereignisprotokoll.</summary>
/// <param name="App">Abgestürzte Anwendung, z. B. „explorer.exe".</param>
/// <param name="Module">Verursachendes Modul, z. B. „windows.storage.dll" (kann leer sein).</param>
/// <param name="When">Zeitpunkt des Absturzes.</param>
public sealed record CrashEvent(string App, string Module, DateTime When);

/// <summary>Zusammengefasste Abstürze einer App/Modul-Kombination.</summary>
public sealed record CrashGroup(
    string App,
    string Module,
    int Count,
    DateTime First,
    DateTime Last);

/// <summary>
/// Fasst Anwendungsabstürze zusammen (App + verursachendes Modul + Häufigkeit +
/// „seit wann") und leitet daraus Ampel und Praxis-Tipp ab. Rein funktional und
/// damit gut testbar – die Ereignisprotokoll-Abfrage liegt in der Infrastruktur.
/// </summary>
public static class AppCrashAnalyzer
{
    /// <summary>Ab dieser Anzahl gleicher Abstürze ist es ein echtes Muster (Warnung).</summary>
    public const int WarnCount = 3;

    /// <summary>Ab dieser Anzahl ist die App praktisch unbenutzbar (kritisch).</summary>
    public const int CriticalCount = 10;

    /// <summary>Gruppiert Abstürze nach App + Modul, häufigste zuerst.</summary>
    public static IReadOnlyList<CrashGroup> Group(IEnumerable<CrashEvent> crashes)
    {
        var groups = new Dictionary<(string App, string Module), (int Count, DateTime First, DateTime Last)>();

        foreach (var c in crashes)
        {
            var app = Normalize(c.App);
            var module = Normalize(c.Module);
            if (app.Length == 0) continue;

            var key = (app, module);
            if (groups.TryGetValue(key, out var acc))
                groups[key] = (acc.Count + 1,
                    c.When < acc.First ? c.When : acc.First,
                    c.When > acc.Last ? c.When : acc.Last);
            else
                groups[key] = (1, c.When, c.When);
        }

        return groups
            .Select(kv => new CrashGroup(kv.Key.App, kv.Key.Module,
                kv.Value.Count, kv.Value.First, kv.Value.Last))
            .OrderByDescending(g => g.Count)
            .ThenByDescending(g => g.Last)
            .ToList();
    }

    /// <summary>Ampel für eine Absturzgruppe.</summary>
    public static Severity SeverityFor(CrashGroup group) => group.Count switch
    {
        >= CriticalCount => Severity.Critical,
        >= WarnCount => Severity.Warning,
        _ => Severity.Info
    };

    /// <summary>
    /// Praxis-Tipp passend zur abgestürzten Anwendung: nennt die konkreten
    /// Reparaturschritte, die diese Symptomatik erfahrungsgemäß beheben.
    /// </summary>
    public static string TipFor(CrashGroup group)
    {
        var app = group.App.ToLowerInvariant();

        if (app.Contains("searchhost") || app.Contains("searchapp") ||
            app.Contains("searchui") || app.Contains("searchindexer"))
            return "Die Windows-Suche stürzt ab. Wirksam ist meist: Reparatur „Windows-Suche zurücksetzen“ " +
                   "(baut den Suchindex neu auf), danach „Systemdateien reparieren (SFC + DISM)“. " +
                   "Beginnen die Abstürze direkt nach einem Update, hilft oft das nächste kumulative Update.";

        if (app.Contains("explorer"))
            return "Der Windows-Explorer stürzt ab. Empfohlen: „Systemdateien reparieren (SFC + DISM)“, " +
                   "„Explorer neu starten“ und – falls es beim Suchen passiert – „Windows-Suche zurücksetzen“. " +
                   "Häufige Ursache sind außerdem Kontextmenü-/Shell-Erweiterungen von Drittprogrammen.";

        if (app.Contains("textinputhost") || app.Contains("ctfmon"))
            return "Die Texteingabe stürzt ab (Symptom: in der Suche lässt sich nichts tippen). " +
                   "Wirksam: Reparatur „Texteingabe neu starten“, anschließend „Systemdateien reparieren“.";

        if (app.Contains("msedge") || app.Contains("chrome") || app.Contains("firefox"))
            return "Der Browser stürzt ab. Zuerst Browser aktualisieren und Erweiterungen testweise deaktivieren. " +
                   "Stürzen zusätzlich Explorer/Suche ab, liegt die Ursache meist im System – dann " +
                   "„Systemdateien reparieren (SFC + DISM)“ ausführen.";

        return "Wiederholte Abstürze derselben Anwendung deuten auf beschädigte Systemdateien, einen " +
               "fehlerhaften Treiber oder ein defektes Update hin. Empfohlen: „Systemdateien reparieren " +
               "(SFC + DISM)“ und die Anwendung aktualisieren/neu installieren.";
    }

    /// <summary>Anzeigetext, z. B. „14x seit 09.07. (zuletzt 11.07. 21:04)".</summary>
    public static string Describe(CrashGroup group)
        => $"{group.Count}x seit {group.First:dd.MM.} (zuletzt {group.Last:dd.MM. HH:mm})";

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
