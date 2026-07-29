using DariaTech.PcDoctor.Fixes;

namespace DariaTech.PcDoctor.Core.Symptoms;

/// <summary>
/// Kuratierte Symptomliste für den Symptom-Assistenten: ordnet jeder typischen
/// Kundenmeldung die relevanten Prüfungen und die passenden Reparaturen zu.
/// Rein deklarativ (keine UI, keine I/O) und damit gut testbar.
///
/// Die Bereichsnamen müssen exakt den <see cref="ICheck.Area"/>-Werten
/// entsprechen – ein Tippfehler würde dazu führen, dass nichts geprüft wird.
/// Ein Test sichert das ab.
/// </summary>
public static class SymptomCatalog
{
    // Bereichsnamen zentral, damit sie nicht mehrfach getippt werden.
    private const string AreaCrashes = "Programmabstürze (letzte 30 Tage)";
    private const string AreaUpdateStability = "Updates & Stabilität";
    private const string AreaEventLog = "Ereignisprotokoll (letzte 7 Tage)";
    private const string AreaSystem = "System & Betriebssystem";
    private const string AreaCpuMemory = "Prozessor & Arbeitsspeicher";
    private const string AreaDiskSpace = "Datenträger – Speicherplatz";
    private const string AreaDiskHogs = "Speicherplatz-Fresser";
    private const string AreaDiskType = "Datenträger – Typ";
    private const string AreaDiskHealth = "Datenträger – Gesundheit (SMART)";
    private const string AreaDiskDetail = "Datenträger – Detail";
    private const string AreaStartup = "Autostart-Programme";
    private const string AreaPrograms = "Installierte Programme";
    private const string AreaNetwork = "Netzwerk";
    private const string AreaNetworkQuality = "Netzwerk-Qualität";
    private const string AreaDrivers = "Treiber & Geräte";
    private const string AreaWindowsUpdate = "Windows-Updates";
    private const string AreaSecurity = "Windows-Sicherheit";
    private const string AreaBackup = "Datensicherung";

    public static IReadOnlyList<Symptom> All { get; } = new List<Symptom>
    {
        new(
            Id: "search",
            Title: "Windows-Suche geht nicht / keine Eingabe möglich",
            Question: "In der Windows-Suche lässt sich nichts tippen oder es kommen keine Ergebnisse.",
            CheckAreas: new[] { AreaCrashes, AreaUpdateStability, AreaEventLog },
            FixTypes: new[]
            {
                typeof(TextInputRestartFix),      // harmlos, wirkt sofort
                typeof(WindowsSearchResetFix),    // Suchindex neu
                typeof(SystemFileRepairFix)       // Systemdateien
            },
            Advice: "Zuerst „Texteingabe neu starten“ – behebt die fehlende Eingabe meist sofort. " +
                    "Hilft das nicht, „Windows-Suche zurücksetzen“ (Index wird neu aufgebaut) und " +
                    "danach „Systemdateien reparieren“. Weist die Kachel „Updates & Stabilität“ einen " +
                    "Update-Verdacht aus, zusätzlich nach neueren Updates suchen."),

        new(
            Id: "explorer-crash",
            Title: "Explorer/Fenster schließen sich von selbst",
            Question: "Der Datei-Explorer oder ein Programmfenster schließt sich nach kurzer Zeit von selbst.",
            CheckAreas: new[] { AreaCrashes, AreaUpdateStability, AreaEventLog, AreaDiskHealth },
            FixTypes: new[]
            {
                typeof(RestartExplorerFix),
                typeof(SystemFileRepairFix),
                typeof(WindowsSearchResetFix)
            },
            Advice: "Die Kachel „Programmabstürze“ nennt die abstürzende Anwendung und das verursachende " +
                    "Modul – das ist der wichtigste Hinweis. Passiert es beim Suchen, ist meist der " +
                    "Suchindex beschädigt. Standardweg: „Systemdateien reparieren (SFC + DISM)“, dann " +
                    "„Windows-Suche zurücksetzen“."),

        new(
            Id: "slow",
            Title: "PC ist langsam",
            Question: "Der Rechner startet langsam und reagiert insgesamt zäh.",
            CheckAreas: new[]
            {
                AreaDiskType, AreaDiskSpace, AreaDiskHogs, AreaStartup,
                AreaCpuMemory, AreaPrograms, AreaSystem
            },
            FixTypes: new[]
            {
                typeof(ClearTempFilesFix),
                typeof(ClearAppCacheFix),
                typeof(RemoveBloatwareFix),
                typeof(PowerPlanHighPerformanceFix)
            },
            Advice: "Wichtigste Frage: SSD oder HDD? Läuft das System auf einer klassischen Festplatte " +
                    "(Kachel „Datenträger – Typ“), bringt nur der Umstieg auf eine SSD echte Besserung – " +
                    "alles andere sind Feinheiten. Sonst: Platz freigeben, Autostart entschlacken, " +
                    "Werbe-Apps entfernen."),

        new(
            Id: "network",
            Title: "Kein Internet / Internet langsam",
            Question: "Webseiten laden nicht oder sehr langsam, WLAN bricht ab.",
            CheckAreas: new[] { AreaNetwork, AreaNetworkQuality, AreaDrivers },
            FixTypes: new[]
            {
                typeof(SpeedTestFix),
                typeof(FlushDnsFix),
                typeof(WinsockResetFix),
                typeof(NetworkResetFix)
            },
            Advice: "Erst messen (Speedtest) und die Kachel „Netzwerk-Qualität“ lesen: schwaches WLAN-Signal " +
                    "oder hohe Latenz erklären das Problem meist schon. Bei Namensauflösungs-Problemen " +
                    "„DNS-Cache leeren“. Winsock-/Netzwerk-Reset sind die letzten Schritte (Neustart nötig)."),

        new(
            Id: "printer",
            Title: "Drucker druckt nicht",
            Question: "Druckaufträge bleiben in der Warteschlange hängen.",
            CheckAreas: new[] { AreaDrivers, AreaNetwork },
            FixTypes: new[] { typeof(PrinterSpoolerResetFix) },
            Advice: "„Druckerspooler zurücksetzen“ löst hängende Warteschlangen. Bleibt es dabei, in der " +
                    "Kachel „Treiber & Geräte“ nach Problemgeräten sehen und den Druckertreiber neu " +
                    "installieren; bei Netzwerkdruckern zusätzlich die Verbindung prüfen."),

        new(
            Id: "crashes",
            Title: "PC stürzt ab / startet unerwartet neu",
            Question: "Bluescreens, Einfrieren oder plötzliche Neustarts.",
            CheckAreas: new[]
            {
                AreaEventLog, AreaCrashes, AreaDiskHealth, AreaDiskDetail,
                AreaCpuMemory, AreaUpdateStability
            },
            FixTypes: new[]
            {
                typeof(MemoryDiagnosticFix),
                typeof(SystemFileRepairFix),
                typeof(CheckDiskFix)
            },
            Advice: "Zuerst die Kachel „Ereignisprotokoll“ auf Kernel-Power 41 (unerwartete Abschaltung) " +
                    "und „Datenträger – Gesundheit“ prüfen – eine sterbende SSD/HDD ist eine häufige " +
                    "Ursache. Danach Arbeitsspeicher testen und Systemdateien reparieren. Bei " +
                    "Überhitzungsverdacht den Tab „Gaming & Stresstest“ nutzen."),

        new(
            Id: "update-fail",
            Title: "Windows-Updates lassen sich nicht installieren",
            Question: "Updates schlagen fehl oder hängen bei der Installation.",
            CheckAreas: new[] { AreaWindowsUpdate, AreaUpdateStability, AreaDiskSpace, AreaSecurity },
            FixTypes: new[]
            {
                typeof(WindowsUpdateRepairFix),
                typeof(SystemFileRepairFix),
                typeof(ClearTempFilesFix)
            },
            Advice: "Häufigste Ursachen: zu wenig freier Speicherplatz oder ein beschädigter " +
                    "Update-Zwischenspeicher. Erst Platz prüfen/freigeben, dann " +
                    "„Windows-Update reparieren“ ausführen (setzt die Update-Komponenten zurück, " +
                    "ein Wiederherstellungspunkt wird vorher angelegt)."),

        new(
            Id: "data-safety",
            Title: "Wichtige Daten sind in Gefahr / kein Backup",
            Question: "Der PC macht Probleme und es ist unklar, ob die Daten gesichert sind.",
            CheckAreas: new[] { AreaBackup, AreaDiskHealth, AreaDiskDetail, AreaDiskSpace },
            FixTypes: Array.Empty<Type>(),
            Advice: "Vor allen weiteren Reparaturen klären: Gibt es eine Sicherung (Kachel " +
                    "„Datensicherung“)? Meldet „Datenträger – Gesundheit“ einen Ausfall, zuerst die " +
                    "Daten sichern bzw. den Datenträger über den Tab „Klonen“ 1:1 kopieren – " +
                    "erst danach reparieren."),
    };

    /// <summary>Findet ein Symptom über seinen Schlüssel (oder <c>null</c>).</summary>
    public static Symptom? ById(string? id)
        => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
}
