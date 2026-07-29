using System.Text.RegularExpressions;

namespace DariaTech.PcDoctor.Core.Security;

/// <summary>
/// Bewertet, ob ein Autostart-/Aufgaben-Pfad an einem für Schadsoftware
/// typischen Ort liegt. Bewusst als „Indiz“, nicht als Urteil: Auch legitime
/// Programme starten gelegentlich aus dem Benutzerprofil. Rein funktional und
/// damit vollständig testbar.
/// </summary>
public static class SuspiciousLocationRules
{
    /// <summary>Ordner, aus denen seriöse Software praktisch nie dauerhaft startet.</summary>
    private static readonly (string Fragment, string Reason)[] SuspiciousFolders =
    {
        (@"\appdata\local\temp\", "Start aus dem temporären Ordner – seriöse Programme liegen dort nicht dauerhaft."),
        (@"\windows\temp\",       "Start aus dem Windows-Temp-Ordner – untypisch für installierte Software."),
        (@"\$recycle.bin\",       "Start aus dem Papierkorb – praktisch immer Schadsoftware."),
        (@"\users\public\",       "Start aus dem öffentlichen Benutzerordner – untypischer Ort."),
        (@"\downloads\",          "Start direkt aus dem Downloads-Ordner – untypisch für dauerhafte Software."),
        (@"\programdata\temp",    "Start aus einem temporären ProgramData-Ordner."),
    };

    /// <summary>Doppelte Endung wie „rechnung.pdf.exe“ – klassische Tarnung.</summary>
    private static readonly Regex DoubleExtension = new(
        @"\.(pdf|doc|docx|xls|xlsx|jpg|jpeg|png|txt|rtf|zip)\.(exe|scr|com|pif|bat|cmd|js|vbs)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Bewusst KEINE Erkennung „zufällig aussehender Dateinamen“: Eine Längen-/
    // Zeichenheuristik stuft zu viele legitime Programme falsch ein (geprüft:
    // OneDrive.exe, explorer.exe, Telegram.exe, thunderbird.exe, AcroRd32.exe …).
    // Ein Indikator mit hoher Falsch-Positiv-Rate ist schlechter als keiner, weil
    // er das Vertrauen in alle übrigen Befunde untergräbt.

    /// <summary>Skript-/Ausführungsarten, die in Autostarts besondere Aufmerksamkeit verdienen.</summary>
    private static readonly string[] ScriptHosts =
    {
        "powershell", "pwsh", "wscript", "cscript", "mshta", "rundll32", "regsvr32", "certutil",
    };

    /// <summary>
    /// Prüft einen Autostart-Befehl/Pfad. Liefert die Begründung, wenn er
    /// auffällig ist – sonst <c>null</c>.
    /// </summary>
    public static string? Evaluate(string? commandOrPath)
    {
        if (string.IsNullOrWhiteSpace(commandOrPath)) return null;

        var command = commandOrPath.Trim();
        var lower = command.ToLowerInvariant();
        var fileName = ExtractFileName(lower);

        foreach (var (fragment, reason) in SuspiciousFolders)
            if (lower.Contains(fragment, StringComparison.Ordinal))
                return reason;

        if (DoubleExtension.IsMatch(fileName))
            return "Doppelte Dateiendung (z. B. „…pdf.exe“) – klassische Tarnung von Schadsoftware.";

        // Skript-Hosts mit verschleierten Parametern sind ein starkes Indiz.
        foreach (var host in ScriptHosts)
        {
            if (!lower.Contains(host, StringComparison.Ordinal)) continue;
            if (lower.Contains("-enc", StringComparison.Ordinal) ||
                lower.Contains("-e ", StringComparison.Ordinal) ||
                lower.Contains("frombase64", StringComparison.Ordinal) ||
                lower.Contains("hidden", StringComparison.Ordinal) ||
                lower.Contains("bypass", StringComparison.Ordinal) ||
                lower.Contains("downloadstring", StringComparison.Ordinal) ||
                lower.Contains("iex", StringComparison.Ordinal))
                return $"Autostart führt {host} mit verschleierten Parametern aus – sehr verdächtig.";
        }

        return null;
    }

    /// <summary>
    /// Programmpfad bis zur ausführbaren Endung – erlaubt Leerzeichen im Pfad
    /// („C:\Program Files\…"). Ein einfaches Trennen am ersten Leerzeichen wäre
    /// falsch und hätte den Dateinamen verstümmelt.
    /// </summary>
    private static readonly Regex ExecutablePath = new(
        @"^(?<path>.*?\.(?:exe|com|scr|pif|bat|cmd|js|vbs|dll|msi))(?:\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Liest den Dateinamen aus einer Befehlszeile (Anführungszeichen/Parameter werden entfernt).</summary>
    public static string ExtractFileName(string command)
    {
        var value = command.Trim();

        // In Anführungszeichen gesetzter Pfad: nur diesen Teil betrachten.
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            if (end > 1) value = value[1..end];
        }
        else if (ExecutablePath.Match(value) is { Success: true } match)
        {
            // Bis zur ausführbaren Endung schneiden – Leerzeichen im Pfad bleiben erhalten.
            value = match.Groups["path"].Value;
        }
        else
        {
            // Keine erkennbare Endung: notfalls am ersten Leerzeichen trennen.
            var space = value.IndexOf(' ');
            if (space > 0) value = value[..space];
        }

        var slash = value.LastIndexOfAny(new[] { '\\', '/' });
        return slash >= 0 && slash < value.Length - 1 ? value[(slash + 1)..] : value;
    }
}
