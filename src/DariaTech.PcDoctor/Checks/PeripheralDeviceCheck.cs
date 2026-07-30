using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Geräte nach Kategorie: Audio, Bluetooth, Kamera, USB, Grafik, Netzwerk,
/// Drucker, Eingabegeräte und Akku. Anders als die allgemeine Treiberprüfung
/// beantwortet dieser Check gezielt die Frage „Ist bei MEINEM Problembereich
/// etwas defekt?“ – genau das braucht der Symptom-Assistent.
///
/// Gemeldet wird nur, was Windows selbst als fehlerhaft führt
/// (<c>ConfigManagerErrorCode</c>); ein deaktiviertes Gerät wird eigens
/// hervorgehoben, weil das die häufigste schnell behebbare Ursache ist.
/// Rein lesend.
/// </summary>
public sealed class PeripheralDeviceCheck : ICheck
{
    public string Area => "Geräte nach Bereich";

    private sealed record DeviceInfo(string Name, string Category, int ErrorCode);

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            List<DeviceInfo> devices;

            try
            {
                devices = ReadDevices(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                results.Add(new CheckResult(Area, "Hinweis", "Geräteliste nicht lesbar", Severity.Info));
                return results;
            }

            if (devices.Count == 0)
            {
                results.Add(new CheckResult(Area, "Hinweis", "keine Geräte ermittelbar", Severity.Info));
                return results;
            }

            var problems = devices.Where(d => d.ErrorCode != 0).ToList();

            results.Add(problems.Count == 0
                ? new CheckResult(Area, "Gesamtstatus",
                    $"{devices.Count} Geräte geprüft – keine Probleme", Severity.Ok)
                : new CheckResult(Area, "Gesamtstatus",
                    $"{problems.Count} Gerät(e) mit Problem in " +
                    $"{problems.Select(p => p.Category).Distinct().Count()} Bereich(en)",
                    Severity.Warning,
                    Detail: "Betroffene Bereiche: " +
                            string.Join(", ", problems.Select(p => p.Category).Distinct().OrderBy(c => c)),
                    OpenTarget: "devmgmt.msc"));

            // Je Kategorie eine Zeile – auffällige zuerst.
            foreach (var group in devices
                         .GroupBy(d => d.Category)
                         .OrderByDescending(g => g.Count(d => d.ErrorCode != 0))
                         .ThenBy(g => g.Key))
            {
                var faulty = group.Where(d => d.ErrorCode != 0).ToList();

                if (faulty.Count == 0)
                {
                    results.Add(new CheckResult(Area, group.Key,
                        $"{group.Count()} Gerät(e), keine Probleme", Severity.Ok));
                    continue;
                }

                var disabled = faulty.Where(d => DeviceCategories.IsDisabled(d.ErrorCode)).ToList();
                results.Add(new CheckResult(Area, group.Key,
                    $"{faulty.Count} von {group.Count()} Gerät(en) mit Problem",
                    Severity.Warning,
                    Detail: disabled.Count > 0
                        ? $"{disabled.Count} Gerät(e) sind DEAKTIVIERT – das ist meist mit zwei Klicks behoben."
                        : null,
                    Tip: TipFor(group.Key, disabled.Count > 0),
                    OpenTarget: "devmgmt.msc"));

                foreach (var device in faulty.Take(5))
                    results.Add(new CheckResult(Area, $" – {device.Name}",
                        DeviceCategories.CodeMeaning(device.ErrorCode),
                        Severity.Warning,
                        Detail: $"Geräte-Manager-Code {device.ErrorCode}"));
            }

            return results;
        }, ct);

    /// <summary>Praxis-Tipp passend zur Gerätekategorie.</summary>
    private static string TipFor(string category, bool hasDisabled)
    {
        if (hasDisabled)
            return "Zuerst prüfen: Geräte-Manager öffnen, das Gerät suchen, Rechtsklick → „Gerät aktivieren“. " +
                   "Deaktivierte Geräte sind die häufigste Ursache und sofort behoben.";

        return category switch
        {
            DeviceCategories.Audio =>
                "Empfohlen: Reparatur „Audiodienst neu starten“ ausführen. Hilft das nicht, im " +
                "Geräte-Manager den Audiotreiber deinstallieren und den PC neu starten – Windows " +
                "installiert ihn dann neu.",
            DeviceCategories.Bluetooth =>
                "Empfohlen: Reparatur „Bluetooth-Dienst neu starten“. Bleibt es dabei, Bluetooth-Treiber " +
                "vom Hersteller (Lenovo, HP, Intel) neu installieren – Windows-eigene Treiber sind hier " +
                "oft unvollständig.",
            DeviceCategories.Camera =>
                "Prüfen: Einstellungen → Datenschutz → Kamera (Zugriff erlaubt?), Abdeckung/Schalter am " +
                "Gerät, dann Treiber neu installieren.",
            DeviceCategories.Usb =>
                "Anderen Anschluss und anderes Kabel testen. Bleibt der Fehler, im Geräte-Manager die " +
                "USB-Controller deinstallieren und neu starten.",
            DeviceCategories.Display =>
                "Grafiktreiber direkt beim Hersteller laden (NVIDIA, AMD, Intel) statt über Windows-Update – " +
                "das behebt Flackern und Auflösungsprobleme meist zuverlässig.",
            DeviceCategories.Printer =>
                "Reparatur „Druckerspooler zurücksetzen“ ausführen und den Druckertreiber neu installieren.",
            DeviceCategories.Power =>
                "Akkutreiber (ACPI) im Geräte-Manager deinstallieren und neu starten; zusätzlich den " +
                "Akku-Bericht erstellen lassen.",
            _ => "Im Geräte-Manager: Rechtsklick auf das Gerät → „Treiber aktualisieren“; hilft das nicht, " +
                 "Gerät deinstallieren und neu starten."
        };
    }

    private static List<DeviceInfo> ReadDevices(CancellationToken ct)
    {
        var devices = new List<DeviceInfo>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, PNPClass, ConfigManagerErrorCode FROM Win32_PnPEntity");

        foreach (ManagementBaseObject obj in searcher.Get())
        {
            ct.ThrowIfCancellationRequested();
            using (obj)
            {
                var name = obj["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var category = DeviceCategories.Classify(obj["PNPClass"]?.ToString(), name);
                if (category is null) continue;   // Systemkomponenten überspringen

                var code = obj["ConfigManagerErrorCode"] is null
                    ? 0
                    : Convert.ToInt32(obj["ConfigManagerErrorCode"]);

                devices.Add(new DeviceInfo(name!, category, code));
            }
        }

        return devices;
    }
}
