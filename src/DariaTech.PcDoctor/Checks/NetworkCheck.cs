using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Netzwerk: aktiver Adapter mit Standardgateway, IPv4-Adresse und Gateway
/// (<see cref="NetworkInterface"/>), plus Ping auf 1.1.1.1 als Internet-Test.
/// Kein Internet → Warnung.
/// </summary>
public sealed class NetworkCheck : ICheck
{
    public string Area => "Netzwerk";

    public async Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
    {
        var results = new List<CheckResult>();

        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .FirstOrDefault(n => n.GetIPProperties().GatewayAddresses
                    .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                        && !g.Address.Equals(IPAddress.Any)));

            if (nic is not null)
            {
                var props = nic.GetIPProperties();
                var ipv4 = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    ?.Address.ToString();
                var gateway = props.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                    ?.Address.ToString();

                results.Add(new CheckResult(Area, "Adapter", nic.Name, Severity.Info));
                if (ipv4 is not null)
                    results.Add(new CheckResult(Area, "IPv4", ipv4, Severity.Info));
                if (gateway is not null)
                    results.Add(new CheckResult(Area, "Gateway", gateway, Severity.Info));
            }
        }
        catch
        {
            results.Add(new CheckResult(Area, "Hinweis",
                "Adapterinfo nicht lesbar", Severity.Info));
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(IPAddress.Parse("1.1.1.1"), 2000)
                .WaitAsync(ct).ConfigureAwait(false);
            results.Add(reply.Status == IPStatus.Success
                ? new CheckResult(Area, "Internet", "erreichbar", Severity.Ok)
                : new CheckResult(Area, "Internet", "nicht erreichbar", Severity.Warning,
                    "Internet nicht erreichbar – Verbindung prüfen."));
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            results.Add(new CheckResult(Area, "Internet",
                "nicht erreichbar", Severity.Warning, "Internet nicht erreichbar – Verbindung prüfen."));
        }

        return results;
    }
}
