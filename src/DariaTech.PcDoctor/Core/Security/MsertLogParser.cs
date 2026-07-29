using System.Text.RegularExpressions;

namespace DariaTech.PcDoctor.Core.Security;

/// <summary>Ergebnis eines Microsoft-Safety-Scanner-Durchlaufs.</summary>
/// <param name="ThreatNames">Gefundene Bedrohungen (leer = nichts gefunden).</param>
/// <param name="ReturnCode">Vom Scanner gemeldeter Rückgabewert, falls im Protokoll enthalten.</param>
public sealed record MsertResult(IReadOnlyList<string> ThreatNames, int? ReturnCode)
{
    public bool FoundThreats => ThreatNames.Count > 0;
}

/// <summary>
/// Liest das Protokoll des Microsoft Safety Scanner
/// (<c>%SystemRoot%\debug\msert.log</c>) aus, um das Ergebnis konkret benennen zu
/// können statt nur „fertig“. Rein funktional und damit vollständig testbar.
/// </summary>
public static class MsertLogParser
{
    // Im Protokoll stehen Funde als "Threat detected: <Name>" bzw. "Found <Name>".
    private static readonly Regex ThreatLine = new(
        @"(?:Threat detected|Threat\s*:|Found)\s*:?\s*(?<name>[^\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReturnCodeLine = new(
        @"Return code:\s*(?<code>-?\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NoThreats = new(
        @"no (?:known )?(?:malicious software|threats?|infection)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static MsertResult Parse(string? log)
    {
        if (string.IsNullOrWhiteSpace(log))
            return new MsertResult(Array.Empty<string>(), null);

        int? returnCode = null;
        if (ReturnCodeLine.Match(log) is { Success: true } rc &&
            int.TryParse(rc.Groups["code"].Value, out var code))
            returnCode = code;

        // Meldet das Protokoll ausdrücklich „nichts gefunden“, keine Namen sammeln.
        if (NoThreats.IsMatch(log))
            return new MsertResult(Array.Empty<string>(), returnCode);

        var names = new List<string>();
        foreach (Match match in ThreatLine.Matches(log))
        {
            var name = match.Groups["name"].Value.Trim().TrimEnd('.');
            if (name.Length == 0) continue;
            if (name.Contains("no ", StringComparison.OrdinalIgnoreCase) && name.Length < 40) continue;
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase)) names.Add(name);
        }

        return new MsertResult(names, returnCode);
    }
}
