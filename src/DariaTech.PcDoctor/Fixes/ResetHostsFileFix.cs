using System.IO;
using System.Text;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Security;

namespace DariaTech.PcDoctor.Fixes;

/// <summary>
/// Setzt die Windows-hosts-Datei auf den Standardinhalt zurück. Vorher wird eine
/// Sicherungskopie angelegt – die Aktion ist damit umkehrbar.
///
/// Hintergrund: Schadsoftware trägt in die hosts-Datei Umleitungen ein, um
/// Virenscanner-Updates zu blockieren oder Webseiten auf gefälschte Server zu
/// lenken. Der Standardinhalt enthält nur Kommentare.
/// </summary>
public sealed class ResetHostsFileFix : IFixAction
{
    /// <summary>Standardinhalt wie von Windows ausgeliefert (nur Kommentare).</summary>
    private const string DefaultHosts =
        "# Copyright (c) 1993-2009 Microsoft Corp.\r\n" +
        "#\r\n" +
        "# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.\r\n" +
        "#\r\n" +
        "# This file contains the mappings of IP addresses to host names. Each\r\n" +
        "# entry should be kept on an individual line. The IP address should\r\n" +
        "# be placed in the first column followed by the corresponding host name.\r\n" +
        "# The IP address and the host name should be separated by at least one\r\n" +
        "# space.\r\n" +
        "#\r\n" +
        "# Additionally, comments (such as these) may be inserted on individual\r\n" +
        "# lines or following the machine name denoted by a '#' symbol.\r\n" +
        "#\r\n" +
        "# For example:\r\n" +
        "#\r\n" +
        "#      102.54.94.97     rhino.acme.com          # source server\r\n" +
        "#       38.25.63.10     x.acme.com              # x client host\r\n" +
        "\r\n" +
        "# localhost name resolution is handled within DNS itself.\r\n" +
        "#\t127.0.0.1       localhost\r\n" +
        "#\t::1             localhost\r\n";

    public string Title => "hosts-Datei zurücksetzen";

    public string Description =>
        "Setzt die Windows-hosts-Datei auf den Auslieferungszustand zurück. Damit werden Umleitungen " +
        "und Sperren entfernt, die Schadsoftware dort einträgt (typisch: Virenscanner-Updates werden " +
        "blockiert oder Webseiten auf gefälschte Server gelenkt).\n\n" +
        "Vor der Änderung wird automatisch eine Sicherungskopie angelegt (hosts.dariatech-backup) – " +
        "die Aktion ist also umkehrbar.\n\n" +
        "Achtung: In Firmennetzen sind hosts-Einträge manchmal absichtlich gesetzt. Falls unklar, " +
        "vorher die Einträge in der Kachel „Befall-Indikatoren“ ansehen.";

    public bool RequiresRestorePoint => false;
    public bool IsReversible => true;   // Sicherungskopie wird angelegt

    public Task<FixOutcome> ExecuteAsync(IProgress<string> progress, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var hostsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "drivers", "etc", "hosts");

            if (!File.Exists(hostsPath))
                return new FixOutcome(false, $"Die hosts-Datei wurde nicht gefunden ({hostsPath}).");

            // 1. Sicherungskopie
            var backupPath = hostsPath + ".dariatech-backup";
            try
            {
                File.Copy(hostsPath, backupPath, overwrite: true);
                progress.Report($"Sicherungskopie angelegt: {backupPath}");
            }
            catch (Exception ex)
            {
                return new FixOutcome(false,
                    $"Es konnte keine Sicherungskopie angelegt werden ({ex.Message}). " +
                    "Aus Sicherheitsgründen wurde nichts verändert.");
            }

            // 2. Standardinhalt schreiben
            try
            {
                ct.ThrowIfCancellationRequested();
                File.WriteAllText(hostsPath, DefaultHosts, new UTF8Encoding(false));
                progress.Report("hosts-Datei auf den Windows-Standard zurückgesetzt.");
            }
            catch (Exception ex)
            {
                return new FixOutcome(false,
                    $"Die hosts-Datei konnte nicht geschrieben werden: {ex.Message}. " +
                    "Läuft die App als Administrator? Blockiert ein Virenschutz den Zugriff?");
            }

            // 3. Erfolgskontrolle: enthält die Datei jetzt wirklich keine Einträge mehr?
            try
            {
                var entries = HostsFileAnalyzer.Parse(File.ReadAllText(hostsPath));
                if (entries.Count > 0)
                    return new FixOutcome(false,
                        $"Die Datei enthält weiterhin {entries.Count} Eintrag/Einträge – " +
                        "möglicherweise schreibt ein Programm sie sofort neu. Dann Offlinescan durchführen.");
            }
            catch { /* Prüfung optional */ }

            var msg = "hosts-Datei zurückgesetzt (Sicherungskopie: hosts.dariatech-backup). " +
                      "Zur Sicherheit anschließend „DNS-Cache leeren“ ausführen.";
            progress.Report(msg);
            return new FixOutcome(true, msg);
        }, ct);
}
