using System.Diagnostics;
using System.Net.Http;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Misst Internet-Geschwindigkeit (Download/Upload) und Latenz über die
/// öffentlichen Cloudflare-Speedtest-Endpunkte. Reine Messung, keine
/// Systemänderung.
/// </summary>
public sealed class SpeedTestFix : IFixAction
{
    private const string Down = "https://speed.cloudflare.com/__down?bytes=";
    private const string Up = "https://speed.cloudflare.com/__up";
    private const long DownBytes = 25_000_000;  // 25 MB
    private const int UpBytes = 10_000_000;      // 10 MB

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public string Title => "Internet-Speedtest";
    public string Description =>
        "Misst Download, Upload und Latenz der Internetverbindung über die " +
        "Cloudflare-Speedtest-Server. Es werden testweise einige MB übertragen.";
    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        try
        {
            progress.Report("Messe Latenz …");
            var latency = await LatencyMsAsync(ct).ConfigureAwait(false);
            progress.Report($"Latenz: {latency:0} ms");

            progress.Report("Messe Download …");
            var down = await DownloadMbpsAsync(ct).ConfigureAwait(false);
            progress.Report($"Download: {down:0.0} Mbit/s");

            progress.Report("Messe Upload …");
            var up = await UploadMbpsAsync(ct).ConfigureAwait(false);
            progress.Report($"Upload: {up:0.0} Mbit/s");

            return new FixOutcome(true,
                $"Download {down:0.0} Mbit/s · Upload {up:0.0} Mbit/s · Latenz {latency:0} ms");
        }
        catch (OperationCanceledException)
        {
            return new FixOutcome(false, "Speedtest abgebrochen.");
        }
        catch (Exception ex)
        {
            return new FixOutcome(false, $"Speedtest fehlgeschlagen (offline?): {ex.Message}");
        }
    }

    private static async Task<double> LatencyMsAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var resp = await Http.GetAsync(Down + "0", ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    private static async Task<double> DownloadMbpsAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var resp = await Http.GetAsync(Down + DownBytes,
            HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            total += read;
        sw.Stop();

        return Mbps(total, sw.Elapsed);
    }

    private static async Task<double> UploadMbpsAsync(CancellationToken ct)
    {
        var payload = new byte[UpBytes];
        using var content = new ByteArrayContent(payload);
        var sw = Stopwatch.StartNew();
        using var resp = await Http.PostAsync(Up, content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        sw.Stop();

        return Mbps(UpBytes, sw.Elapsed);
    }

    private static double Mbps(long bytes, TimeSpan elapsed)
        => elapsed.TotalSeconds > 0 ? bytes * 8.0 / elapsed.TotalSeconds / 1_000_000 : 0;
}
