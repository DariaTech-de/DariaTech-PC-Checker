using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Netzwerk-Qualität (ergänzt den <see cref="NetworkCheck"/>): WLAN-Signalstärke,
/// Kanal und Funkstandard (über <c>netsh wlan show interfaces</c>) sowie die
/// Antwortzeit (Latenz) zum Router und ins Internet. Schwaches WLAN oder hohe
/// Latenz/Paketverluste → Warnung. Rein lesend.
/// </summary>
public sealed class NetworkQualityCheck : ICheck
{
    public string Area => "Netzwerk-Qualität";

    public async Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
    {
        var results = new List<CheckResult>();

        await AddWlanQualityAsync(results, ct).ConfigureAwait(false);
        await AddLatencyAsync(results, ct).ConfigureAwait(false);

        if (results.Count == 0)
            results.Add(new CheckResult(Area, "Hinweis", "keine Netzwerkqualität ermittelbar", Severity.Info));

        return results;
    }

    private async Task AddWlanQualityAsync(List<CheckResult> results, CancellationToken ct)
    {
        // Nur auswerten, wenn überhaupt ein WLAN-Adapter aktiv ist.
        var hasWifi = false;
        try
        {
            hasWifi = NetworkInterface.GetAllNetworkInterfaces().Any(n =>
                n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
        }
        catch { /* Adapterabfrage nicht möglich */ }

        if (!hasWifi)
        {
            results.Add(new CheckResult(Area, "WLAN", "nicht aktiv (LAN-Kabel)", Severity.Ok));
            return;
        }

        try
        {
            var netsh = await ProcessRunner.RunAsync("netsh.exe", "wlan show interfaces", progress: null, ct)
                .ConfigureAwait(false);
            var info = WlanInfoParser.Parse(netsh.Output);

            if (info.SignalPercent is int pct)
            {
                var sev = pct >= 70 ? Severity.Ok : pct >= 40 ? Severity.Warning : Severity.Critical;
                var quality = pct >= 70 ? "gut" : pct >= 40 ? "mäßig" : "schwach";
                var detail = sev == Severity.Ok
                    ? null
                    : $"WLAN-Signal {quality} ({pct}%). Schwaches Signal bremst die Geschwindigkeit und " +
                      "verursacht Abbrüche.";
                var tip = sev == Severity.Ok
                    ? null
                    : "So verbessern: näher an den Router, Hindernisse/Störquellen (Mikrowelle, DECT) meiden, " +
                      "ggf. WLAN-Repeater/Mesh einsetzen oder 5-GHz-Band nutzen. Wenn möglich, LAN-Kabel verwenden.";
                var band = info.Radio is not null ? $"{pct}% · {info.Radio}" : $"{pct}%";
                results.Add(new CheckResult(Area, "WLAN-Signal", band, sev, detail, Tip: tip));
            }

            if (info.Ssid is not null)
                results.Add(new CheckResult(Area, "WLAN-Netz (SSID)", info.Ssid, Severity.Info));
            if (info.Channel is int ch)
                results.Add(new CheckResult(Area, "WLAN-Kanal", ch.ToString(), Severity.Info,
                    Detail: "Überfüllte Kanäle (viele Nachbar-WLANs auf demselben Kanal) bremsen die " +
                            "Verbindung. Ein WLAN-Analyzer zeigt freie Kanäle."));
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            results.Add(new CheckResult(Area, "WLAN", "Signal nicht auslesbar", Severity.Info));
        }
    }

    private async Task AddLatencyAsync(List<CheckResult> results, CancellationToken ct)
    {
        var gateway = FirstGateway();
        if (gateway is not null)
        {
            var (avg, loss, sent) = await PingStatsAsync(gateway, 4, ct).ConfigureAwait(false);
            results.Add(BuildLatencyResult("Latenz zum Router", avg, loss, sent,
                "Hohe Antwortzeiten oder Paketverluste im Heimnetz deuten auf WLAN-Störungen oder ein " +
                "überlastetes/defektes Netzwerkgerät hin."));
        }

        var (iAvg, iLoss, iSent) = await PingStatsAsync(IPAddress.Parse("1.1.1.1"), 4, ct).ConfigureAwait(false);
        results.Add(BuildLatencyResult("Latenz ins Internet", iAvg, iLoss, iSent,
            "Hohe Internet-Latenz oder Paketverluste stören Videotelefonie, Streaming und Online-Spiele."));
    }

    private CheckResult BuildLatencyResult(string label, double? avgMs, int loss, int sent, string problemDetail)
    {
        if (avgMs is null)
            return new CheckResult(Area, label, "keine Antwort", Severity.Warning, problemDetail);

        var lossPart = loss > 0 ? $", {loss}/{sent} verloren" : string.Empty;
        var value = $"{avgMs.Value:N0} ms{lossPart}";

        var sev = (loss > 0 || avgMs.Value > 80) ? Severity.Warning
                : avgMs.Value > 30 ? Severity.Info
                : Severity.Ok;

        return new CheckResult(Area, label, value, sev, sev == Severity.Ok ? null : problemDetail);
    }

    private static IPAddress? FirstGateway()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
        }
        catch { return null; }
    }

    private static async Task<(double? AvgMs, int Loss, int Sent)> PingStatsAsync(
        IPAddress address, int count, CancellationToken ct)
    {
        double sum = 0;
        int ok = 0, loss = 0;
        using var ping = new Ping();

        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(address, 1500).WaitAsync(ct).ConfigureAwait(false);
                if (reply.Status == IPStatus.Success) { sum += reply.RoundtripTime; ok++; }
                else loss++;
            }
            catch (OperationCanceledException) { throw; }
            catch { loss++; }
        }

        return (ok > 0 ? sum / ok : null, loss, count);
    }
}
