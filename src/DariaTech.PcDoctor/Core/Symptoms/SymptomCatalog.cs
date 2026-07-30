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
    private const string AreaBitLocker = "Laufwerksverschlüsselung (BitLocker)";
    private const string AreaRestore = "Systemwiederherstellung";
    private const string AreaThreats = "Schadsoftware-Befunde";
    private const string AreaIndicators = "Befall-Indikatoren";
    private const string AreaDevices = "Geräte nach Bereich";
    private const string AreaBattery = "Akku";

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
            Id: "audio",
            Title: "Kein Ton / Mikrofon oder Kopfhörer gehen nicht",
            Question: "Es kommt kein Ton, das Mikrofon wird nicht erkannt oder Kopfhörer funktionieren nicht.",
            CheckAreas: new[] { AreaDevices, AreaDrivers, AreaCrashes, AreaUpdateStability },
            FixTypes: new[] { typeof(RestartAudioServiceFix), typeof(SystemFileRepairFix) },
            Advice: "Zuerst „Audiodienst neu starten“ – das behebt den plötzlichen Tonausfall meistens " +
                    "sofort. Prüfen Sie außerdem, ob im Lautsprechersymbol das richtige Wiedergabegerät " +
                    "gewählt ist (häufigste Ursache nach einem Update). Meldet die Kachel „Geräte nach " +
                    "Bereich“ ein deaktiviertes Audiogerät, dieses im Geräte-Manager aktivieren; bleibt " +
                    "es dabei, den Audiotreiber deinstallieren und neu starten."),

        new(
            Id: "bluetooth",
            Title: "Bluetooth verbindet nicht",
            Question: "Bluetooth-Geräte werden nicht gefunden oder verbinden sich nicht mehr.",
            CheckAreas: new[] { AreaDevices, AreaDrivers, AreaUpdateStability },
            FixTypes: new[] { typeof(RestartBluetoothServiceFix), typeof(SystemFileRepairFix) },
            Advice: "Zuerst „Bluetooth-Dienst neu starten“, dann das Gerät erneut koppeln. Fehlt die " +
                    "Bluetooth-Schaltfläche ganz, liegt es meist am Treiber oder Bluetooth ist im " +
                    "BIOS/UEFI abgeschaltet – die Kachel „Geräte nach Bereich“ zeigt, ob überhaupt ein " +
                    "Bluetooth-Adapter erkannt wird. Herstellertreiber (Intel, Lenovo, HP) sind hier " +
                    "zuverlässiger als die von Windows."),

        new(
            Id: "startmenu",
            Title: "Startmenü oder Taskleiste reagiert nicht",
            Question: "Das Startmenü öffnet sich nicht, die Taskleiste reagiert nicht auf Klicks.",
            CheckAreas: new[] { AreaCrashes, AreaUpdateStability, AreaEventLog, AreaSystem },
            FixTypes: new[]
            {
                typeof(RestartStartMenuFix),
                typeof(RestartExplorerFix),
                typeof(SystemFileRepairFix),
                typeof(WindowsSearchResetFix)
            },
            Advice: "Reihenfolge: „Startmenü & Taskleiste neu starten“ → „Explorer neu starten“ → " +
                    "„Systemdateien reparieren“. Die Kachel „Programmabstürze“ nennt, welcher " +
                    "Oberflächen-Prozess abstürzt – das ist der beste Hinweis. Beginnt es direkt nach " +
                    "einem Update, zusätzlich die Kachel „Updates & Stabilität“ beachten."),

        new(
            Id: "usb-device",
            Title: "USB-Gerät wird nicht erkannt",
            Question: "Ein USB-Stick, Drucker oder anderes Gerät wird nicht erkannt.",
            CheckAreas: new[] { AreaDevices, AreaDrivers, AreaEventLog },
            FixTypes: new[] { typeof(SystemFileRepairFix) },
            Advice: "Erst das Einfache ausschließen: anderen Anschluss und anderes Kabel testen, bei " +
                    "USB-Hubs direkt am PC anstecken. Die Kachel „Geräte nach Bereich“ zeigt, ob Windows " +
                    "das Gerät sieht und mit welchem Fehler. Ein deaktiviertes Gerät (Code 22) lässt sich " +
                    "im Geräte-Manager mit zwei Klicks aktivieren; bei Code 43 meldet die Hardware selbst " +
                    "einen Defekt – dann das Gerät an einem anderen PC gegenprüfen."),

        new(
            Id: "battery",
            Title: "Akku hält nicht durch oder lädt nicht",
            Question: "Der Akku ist schnell leer, lädt nicht oder das Notebook geht ohne Kabel sofort aus.",
            CheckAreas: new[] { AreaBattery, AreaDevices, AreaSystem, AreaEventLog },
            FixTypes: new[] { typeof(BatteryReportFix), typeof(PowerPlanHighPerformanceFix) },
            Advice: "Die Kachel „Akku“ zeigt den Verschleiß: Über 40 % Kapazitätsverlust ist ein Tausch " +
                    "fällig – daran ändert keine Einstellung etwas. Für die Beratung den „Akku-Bericht“ " +
                    "erstellen (zeigt Ladezyklen und Kapazitätsverlauf, gut zum Vorzeigen). Lädt gar " +
                    "nichts, prüfen: richtiges Netzteil, Ladebuchse, und ob unter „Geräte nach Bereich“ " +
                    "ein Akku-Treiberproblem gemeldet wird."),

        new(
            Id: "display",
            Title: "Bildschirm flackert oder falsche Auflösung",
            Question: "Das Bild flackert, ist unscharf, die Auflösung stimmt nicht oder der zweite " +
                      "Bildschirm wird nicht erkannt.",
            CheckAreas: new[] { AreaDevices, AreaDrivers, AreaCrashes, AreaUpdateStability },
            FixTypes: new[] { typeof(SystemFileRepairFix) },
            Advice: "Fast immer der Grafiktreiber: direkt beim Hersteller laden (NVIDIA, AMD, Intel) statt " +
                    "über Windows-Update – die Windows-Fassung ist oft älter und Ursache von Flackern. " +
                    "Vorher Kabel und Anschluss prüfen (bei Flackern oft ein defektes HDMI-/DisplayPort-" +
                    "Kabel). Bei einem Notebook mit zwei Grafikchips zusätzlich prüfen, ob der externe " +
                    "Bildschirm am richtigen Anschluss hängt."),

        new(
            Id: "office",
            Title: "Outlook/Office startet nicht oder hängt",
            Question: "Outlook, Word oder Excel startet nicht, friert ein oder stürzt ab.",
            CheckAreas: new[] { AreaCrashes, AreaUpdateStability, AreaPrograms, AreaDiskSpace },
            FixTypes: new[] { typeof(SystemFileRepairFix), typeof(ClearTempFilesFix) },
            Advice: "Die Kachel „Programmabstürze“ nennt das verursachende Modul – oft ein Add-In. " +
                    "Erster Test: Office im abgesicherten Modus starten (Windows-Taste + R → " +
                    "„outlook /safe“ bzw. „winword /safe“). Startet es dort, liegt es an einem Add-In. " +
                    "Sonst über Einstellungen → Apps → Microsoft 365 → „Ändern“ die Office-" +
                    "Schnellreparatur ausführen (bei Bedarf danach die Online-Reparatur)."),

        new(
            Id: "malware",
            Title: "Verdacht auf Virus / Werbe-Popups / PC verhält sich seltsam",
            Question: "Es erscheinen Werbefenster, die Startseite wurde geändert oder der PC verhält sich " +
                      "merkwürdig – Verdacht auf Schadsoftware.",
            CheckAreas: new[]
            {
                AreaThreats, AreaIndicators, AreaSecurity, AreaStartup, AreaPrograms, AreaCrashes
            },
            FixTypes: new[]
            {
                typeof(DefenderSignatureUpdateFix),   // ohne aktuelle Signaturen ist alles wertlos
                typeof(DefenderFullScanFix),
                typeof(DefenderRemoveThreatsFix),
                typeof(ResetHostsFileFix),
                typeof(DefenderOfflineScanFix),       // hartnäckige Fälle (Neustart)
                typeof(SafetyScannerFix)              // unabhängige Zweitmeinung
            },
            Advice: "Feste Reihenfolge: 1) „Virensignaturen aktualisieren“ – ohne aktuelle Signaturen " +
                    "findet kein Scan neue Schädlinge. 2) „Defender-Vollscan“. 3) Bei Funden " +
                    "„Erkannte Bedrohungen entfernen“. 4) Kommt die Bedrohung wieder oder ist der " +
                    "Echtzeitschutz aus, „Defender-Offlinescan“ (Neustart, entfernt tief verankerte " +
                    "Schädlinge). 5) Als unabhängige Zweitmeinung den „Microsoft Safety Scanner“. " +
                    "Wichtig: Die Kachel „Befall-Indikatoren“ prüfen – abgeschalteter Schutz, " +
                    "Scan-Ausnahmen und manipulierte hosts-Datei sind starke Befallzeichen, die ein " +
                    "Scan allein nicht meldet."),

        new(
            Id: "data-safety",
            Title: "Wichtige Daten sind in Gefahr / kein Backup",
            Question: "Der PC macht Probleme und es ist unklar, ob die Daten gesichert sind.",
            CheckAreas: new[] { AreaBackup, AreaBitLocker, AreaRestore, AreaDiskHealth, AreaDiskDetail, AreaDiskSpace },
            FixTypes: new[] { typeof(BitLockerRecoveryKeyFix), typeof(EnableSystemRestoreFix) },
            Advice: "Vor allen weiteren Reparaturen klären: Gibt es eine Sicherung (Kachel " +
                    "„Datensicherung“)? Meldet „Datenträger – Gesundheit“ einen Ausfall, zuerst die " +
                    "Daten sichern bzw. den Datenträger über den Tab „Klonen“ 1:1 kopieren – " +
                    "erst danach reparieren."),
    };

    /// <summary>Findet ein Symptom über seinen Schlüssel (oder <c>null</c>).</summary>
    public static Symptom? ById(string? id)
        => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
}
