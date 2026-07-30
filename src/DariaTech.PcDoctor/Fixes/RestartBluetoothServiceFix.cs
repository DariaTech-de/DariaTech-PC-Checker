using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Infrastructure;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Startet den Bluetooth-Unterstützungsdienst (<c>bthserv</c>) neu. Behebt
/// häufig, dass Geräte nicht mehr gefunden oder nicht verbunden werden.
/// Bereits gekoppelte Geräte bleiben erhalten.
/// </summary>
public sealed class RestartBluetoothServiceFix : IFixAction
{
    public string Title => "Bluetooth-Dienst neu starten";

    public string Description =>
        "Startet den Bluetooth-Unterstützungsdienst neu. Hilft, wenn Bluetooth-Geräte nicht mehr " +
        "gefunden werden, sich nicht verbinden lassen oder die Bluetooth-Schaltfläche fehlt. " +
        "Bereits gekoppelte Geräte bleiben gespeichert – es wird nichts entkoppelt und nichts gelöscht. " +
        "Bestehende Verbindungen (z. B. Kopfhörer) trennen sich kurz und verbinden sich neu.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;   // reiner Dienst-Neustart

    public async Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Stoppe Bluetooth-Dienst …");
        await ProcessRunner.RunAsync("net.exe", "stop bthserv /y", progress, ct).ConfigureAwait(false);

        progress.Report("Starte Bluetooth-Dienst …");
        var start = await ProcessRunner.RunAsync("net.exe", "start bthserv", progress, ct)
            .ConfigureAwait(false);

        if (start.ExitCode == 0)
        {
            const string ok = "Bluetooth-Dienst neu gestartet. Bitte das Gerät erneut verbinden. " +
                              "Wird weiterhin nichts gefunden: Bluetooth in den Einstellungen aus- und " +
                              "wieder einschalten und den Bluetooth-Treiber des Herstellers installieren.";
            progress.Report(ok);
            return new FixOutcome(true, ok);
        }

        return new FixOutcome(false,
            "Der Bluetooth-Dienst konnte nicht gestartet werden. Möglicherweise besitzt dieser PC kein " +
            "Bluetooth oder es ist im BIOS/UEFI abgeschaltet. Bitte im Geräte-Manager prüfen, ob ein " +
            "Bluetooth-Adapter vorhanden ist.");
    }
}
