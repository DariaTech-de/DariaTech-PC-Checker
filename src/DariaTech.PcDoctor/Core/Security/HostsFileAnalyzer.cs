namespace DariaTech.PcDoctor.Core.Security;

/// <summary>Ein Eintrag der Windows-hosts-Datei.</summary>
public sealed record HostsEntry(string Ip, string Host, int LineNumber);

/// <summary>Ein auffälliger hosts-Eintrag mit Begründung.</summary>
public sealed record HostsFinding(HostsEntry Entry, string Reason, Severity Severity);

/// <summary>
/// Untersucht die hosts-Datei auf typische Schadsoftware-Manipulationen.
/// Klassiker: Virenscanner- und Windows-Update-Server werden umgeleitet oder
/// blockiert, damit sich der Schädling nicht entfernen lässt. Rein funktional
/// (Text rein, Befunde raus) und damit vollständig testbar.
/// </summary>
public static class HostsFileAnalyzer
{
    /// <summary>Domänen von Sicherheitsanbietern und Update-Diensten (Blockade = starkes Warnsignal).</summary>
    private static readonly string[] SecurityDomains =
    {
        "windowsupdate", "update.microsoft", "defender", "microsoft.com",
        "malwarebytes", "virustotal", "kaspersky", "avast", "avg.com", "avira",
        "bitdefender", "eset", "norton", "symantec", "mcafee", "sophos",
        "trendmicro", "drweb", "emsisoft", "gdatasoftware", "f-secure",
        "clamav", "adaware", "spybot", "superantispyware",
    };

    /// <summary>Zerlegt den Inhalt der hosts-Datei in Einträge (Kommentare werden übersprungen).</summary>
    public static IReadOnlyList<HostsEntry> Parse(string? content)
    {
        var entries = new List<HostsEntry>();
        if (string.IsNullOrWhiteSpace(content)) return entries;

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Kommentar am Zeilenende abschneiden, reine Kommentarzeilen überspringen.
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var ip = parts[0];
            // Eine Zeile kann mehrere Hostnamen zu einer IP enthalten.
            for (var h = 1; h < parts.Length; h++)
                entries.Add(new HostsEntry(ip, parts[h], i + 1));
        }

        return entries;
    }

    /// <summary>Bewertet die Einträge und liefert die auffälligen zurück.</summary>
    public static IReadOnlyList<HostsFinding> Analyze(IEnumerable<HostsEntry> entries)
    {
        var findings = new List<HostsFinding>();

        foreach (var entry in entries)
        {
            var host = entry.Host.ToLowerInvariant();
            var isSecurityDomain = SecurityDomains.Any(d => host.Contains(d, StringComparison.Ordinal));
            var loopback = IsLoopback(entry.Ip);

            if (isSecurityDomain && loopback)
            {
                findings.Add(new HostsFinding(entry,
                    "Sicherheits-/Update-Server ist über die hosts-Datei blockiert. Das ist ein typischer " +
                    "Schadsoftware-Trick, damit sich der Schädling nicht entfernen lässt.",
                    Severity.Critical));
                continue;
            }

            if (isSecurityDomain)
            {
                findings.Add(new HostsFinding(entry,
                    $"Sicherheits-/Update-Server wird auf {entry.Ip} umgeleitet – sehr verdächtig.",
                    Severity.Critical));
                continue;
            }

            if (!loopback)
            {
                findings.Add(new HostsFinding(entry,
                    $"Umleitung von {entry.Host} auf {entry.Ip}. Solche Einträge können Webseiten " +
                    "auf gefälschte Server lenken – prüfen, ob sie gewollt sind (z. B. Firmennetz).",
                    Severity.Warning));
            }
        }

        return findings;
    }

    /// <summary>True für 127.x.x.x, ::1 und 0.0.0.0 (klassische „blockieren“-Adressen).</summary>
    public static bool IsLoopback(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        var value = ip.Trim();
        return value.StartsWith("127.", StringComparison.Ordinal)
            || value == "::1"
            || value == "0.0.0.0";
    }
}
