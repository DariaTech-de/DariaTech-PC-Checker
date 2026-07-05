using System.Management;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.Checks;

/// <summary>
/// Datenträger-Typ über <c>MSFT_PhysicalDisk</c> (root\Microsoft\Windows\Storage):
/// SSD oder klassische Festplatte (HDD), Modell und Größe. Läuft das System auf
/// einer reinen HDD (keine SSD verbaut), ist das die häufigste Ursache für einen
/// insgesamt langsamen PC → Warnung mit SSD-Upgrade-Empfehlung. Rein lesend.
/// </summary>
public sealed class DiskMediaCheck : ICheck
{
    public string Area => "Datenträger – Typ";

    public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<CheckResult>>(() =>
        {
            var results = new List<CheckResult>();
            var hasSsd = false;
            var hasHdd = false;

            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                var query = new ObjectQuery(
                    "SELECT FriendlyName, MediaType, SpindleSpeed, Size FROM MSFT_PhysicalDisk");
                using var searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementBaseObject disk in searcher.Get())
                {
                    ct.ThrowIfCancellationRequested();

                    var mediaType = disk["MediaType"] is null ? 0 : Convert.ToInt32(disk["MediaType"]);
                    uint? spindle = disk["SpindleSpeed"] is null ? null : Convert.ToUInt32(disk["SpindleSpeed"]);
                    var media = DiskMediaClassifier.Classify(mediaType, spindle);

                    if (media == DiskMediaClassifier.Media.Ssd) hasSsd = true;
                    if (media == DiskMediaClassifier.Media.Hdd) hasHdd = true;

                    var name = disk["FriendlyName"]?.ToString() ?? "Datenträger";
                    var size = disk["Size"] is null ? 0L : Convert.ToInt64(disk["Size"]);
                    var sizeText = size > 0 ? $" · {ByteSize.Human(size)}" : string.Empty;

                    results.Add(new CheckResult(Area, name,
                        $"{DiskMediaClassifier.Label(media)}{sizeText}",
                        media == DiskMediaClassifier.Media.Hdd ? Severity.Info : Severity.Ok));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new CheckResult(Area, "Hinweis", "Datenträgertyp nicht prüfbar", Severity.Info));
                return results;
            }

            if (results.Count == 0)
            {
                results.Add(new CheckResult(Area, "Hinweis", "keine Datenträger erkannt", Severity.Info));
                return results;
            }

            // Nur eine HDD und keine SSD → System läuft sehr wahrscheinlich auf der Festplatte.
            if (hasHdd && !hasSsd)
                results.Insert(0, new CheckResult(Area, "Gesamtbewertung",
                    "System läuft auf einer klassischen Festplatte (HDD)", Severity.Warning,
                    Detail: "Es ist keine SSD verbaut. Eine HDD ist die häufigste Ursache für lange " +
                            "Startzeiten und allgemein zähes Arbeiten – der Umstieg auf eine SSD bringt " +
                            "meist den größten spürbaren Geschwindigkeitsgewinn.",
                    Tip: "Empfehlung: Aufrüstung auf eine SSD (bestehendes System kann per Klon-Funktion " +
                         "dieser App 1:1 übertragen werden).",
                    OpenTarget: null));

            return results;
        }, ct);
}
