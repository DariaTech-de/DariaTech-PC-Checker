using System.Text.RegularExpressions;

namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Liest die relevanten Felder aus der Ausgabe von
/// <c>netsh wlan show interfaces</c>. Sprachunabhängig, soweit möglich
/// (deutsche und englische Windows-Ausgaben). Rein funktional, gut testbar.
/// </summary>
public static class WlanInfoParser
{
    public sealed record WlanInfo(int? SignalPercent, string? Ssid, int? Channel, string? Radio);

    // Signal: Label „Signal" ist in DE und EN identisch, Wert als Prozent.
    private static readonly Regex SignalRx = new(@"(?m)^\s*Signal\s*:\s*(\d{1,3})\s*%", RegexOptions.Compiled);
    // SSID (nicht BSSID): Zeilenanfang, direkt „SSID".
    private static readonly Regex SsidRx = new(@"(?m)^\s*SSID\s*:\s*(.+?)\s*$", RegexOptions.Compiled);
    // Kanal (DE) / Channel (EN).
    private static readonly Regex ChannelRx = new(@"(?m)^\s*(?:Kanal|Channel)\s*:\s*(\d{1,3})", RegexOptions.Compiled);
    // Funktyp (DE) / Radio type (EN).
    private static readonly Regex RadioRx = new(@"(?m)^\s*(?:Funktyp|Radio type)\s*:\s*(.+?)\s*$", RegexOptions.Compiled);

    public static WlanInfo Parse(string? netshOutput)
    {
        var text = netshOutput ?? string.Empty;

        int? signal = null;
        if (SignalRx.Match(text) is { Success: true } sm &&
            int.TryParse(sm.Groups[1].Value, out var s))
            signal = Math.Clamp(s, 0, 100);

        int? channel = null;
        if (ChannelRx.Match(text) is { Success: true } cm &&
            int.TryParse(cm.Groups[1].Value, out var c))
            channel = c;

        var ssid = SsidRx.Match(text) is { Success: true } ssm ? ssm.Groups[1].Value : null;
        var radio = RadioRx.Match(text) is { Success: true } rm ? rm.Groups[1].Value : null;

        return new WlanInfo(signal, string.IsNullOrWhiteSpace(ssid) ? null : ssid, channel,
            string.IsNullOrWhiteSpace(radio) ? null : radio);
    }
}
