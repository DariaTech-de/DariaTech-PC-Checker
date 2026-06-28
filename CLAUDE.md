# DariaTech PC-Doktor — Projektkontext für Claude Code

> Arbeitsanweisung: Lies dieses Dokument vollständig, bevor du Code schreibst.
> Baue die App **inkrementell** entlang der Meilensteine unten. Halte dich an
> die Architektur- und Sicherheitsregeln. Frage nach, wenn etwas unklar ist.
> (Der Produktname „PC-Doktor" ist ein Platzhalter und an einer Stelle änderbar.)

## Was wir bauen

Eine native **Windows-Desktop-App (.NET 8 / WPF)**, die den Zustand eines
Windows-PCs **visuell als Ampel-Dashboard** anzeigt und gängige Probleme
**per Klick behebt**. Zielgruppe: DariaTech-Technik bei Privatkunden-Einsätzen,
aber so einfach bedienbar, dass auch Nicht-Profis damit zurechtkommen.

Die App läuft als **eine portable `.exe`** vom USB-Stick (keine Installation),
fordert Adminrechte an (UAC) und ist signierbar. Sie soll außerdem einen
**HTML-Bericht** exportieren, den man dem Kunden aushändigen kann.

Die Diagnose-Logik existiert bereits als PowerShell-Prototyp (`PC-Schnellcheck.ps1`,
liegt dem Projekt bei) — die Prüfungen werden hier **nativ in C#** nachgebaut.

## Designziele / Prinzipien

- **Einfach bedienbar:** großes Dashboard, klare Ampel, ein Klick pro Fix, deutsche Beschriftung.
- **Sicher:** keine destruktive Aktion ohne Bestätigung; vor systemverändernden Reparaturen automatisch einen Systemwiederherstellungspunkt anlegen; alles protokollieren; Aktionen wo möglich umkehrbar.
- **Robust & portabel:** self-contained Single-File-`.exe`, keine .NET-Runtime-Installation beim Kunden nötig.
- **Professionell:** signierbar, sauberer DariaTech-Look (Navy/Signalblau), exportierbarer HTML-Bericht.

## Tech-Stack (festgelegt)

- **.NET 8**, C#, `net8.0-windows`
- **UI:** WPF + **WPF-UI** (lepoco/wpfui) für Fluent/Win11-Look
- **MVVM:** `CommunityToolkit.Mvvm` (source-generated `[ObservableProperty]` / `[RelayCommand]`)
- **DI/Hosting:** `Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection`
- **Systemzugriff:** `System.Management` (WMI) und direkte .NET/Win32-APIs; nur wo nötig PowerShell/COM (z. B. Windows-Update-Suche via WUApiLib)
- **Logging:** `Serilog` (Datei + In-App-Ansicht)
- **Tests:** `xUnit` für die Check-/Fix-Logik (UI-frei testbar)

## Architektur

UI strikt von Logik getrennt. Checks und Fixes haben **keine** UI-Abhängigkeit.

**Core (kein UI):**
- `ICheck` — eine Diagnose-Prüfung, liefert eine Liste `CheckResult`.
- `CheckResult` — Bereich, Label, Wert, `Severity` (Ok/Info/Warning/Critical), optional Detailtext und zugeordnete `IFixAction`(s).
- `IFixAction` — eine Reparatur. Eigenschaften: `Title`, `Description`, `RequiresRestorePoint`, `IsReversible`; Methode `ExecuteAsync(IProgress<string>, CancellationToken)` → `FixOutcome`.
- `DiagnosticEngine` — führt alle registrierten Checks aus, sammelt Ergebnisse, fängt Fehler je Check ab (ein Fehler bricht den Scan nicht ab), ermittelt die Gesamt-Ampel.
- `RepairService` — führt Fixes aus: legt (falls verlangt) einen Wiederherstellungspunkt an, protokolliert, meldet Fortschritt.
- `ReportExporter` — erzeugt den HTML-Bericht (Stil wie im PowerShell-Skript: Navy-Header, Ampel-Zusammenfassung, Tabellen).

**Ordner:**
- `Checks/` — je eine Klasse pro Prüfung, implementiert `ICheck`.
- `Fixes/` — je eine Klasse pro Reparatur, implementiert `IFixAction`.
- `Core/` — Abstraktionen + Engine + Services.
- `UI/Views/`, `UI/ViewModels/` — WPF.
- `Models/`, `Infrastructure/`.

Alle Checks/Fixes werden im DI-Container registriert und vom `DiagnosticEngine`
bzw. der UI über `IEnumerable<ICheck>` eingesammelt.

## Diagnose-Prüfungen (aus PC-Schnellcheck.ps1 portieren)

Pro Prüfung steht dabei, was gelesen wird und welche Ampel-Regel gilt:

- **System & OS** — `Win32_ComputerSystem` / `Win32_OperatingSystem` / `Win32_BIOS`: Modell, Seriennummer, Windows-Build, Installationsdatum, Uptime. Uptime > 14 Tage → Warnung (Neustart empfohlen).
- **CPU & RAM** — `Win32_Processor` (Auslastung > 90 % → Warnung), OS-Memory (Belegung > 90 % → Warnung), `Win32_PhysicalMemory` (Anzahl Riegel).
- **Datenträger – Speicherplatz** — `Win32_LogicalDisk` (DriveType=3): < 15 % frei → Warnung, < 10 % frei → Kritisch.
- **Datenträger – Gesundheit (SMART)** — `MSFT_PhysicalDisk` (`root\Microsoft\Windows\Storage`) `HealthStatus`; zusätzlich `MSStorageDriver_FailurePredictStatus` (`root\wmi`): `PredictFailure` = true → Kritisch (sofort Backup/Tausch).
- **Akku** (nur Notebooks) — `BatteryStaticData` + `BatteryFullChargedCapacity` (`root\wmi`) → Verschleiß %. > 40 % → Warnung.
- **Windows-Sicherheit** — `MSFT_MpComputerStatus` (`root\Microsoft\Windows\Defender`): Echtzeitschutz aus → Kritisch; Signaturen > 7 Tage alt → Warnung.
- **Windows-Updates** — WUApiLib (`Microsoft.Update.Session`), Suche `IsInstalled=0 and Type='Software'` → Anzahl ausstehend (Warnung). **Langsam** → eigener, abbrechbarer Task; per Option überspringbar.
- **Autostart** — `Win32_StartupCommand`: Anzahl + Liste; > 15 Einträge → Hinweis.
- **Treiber/Geräte** — `Win32_PnPEntity` mit `ConfigManagerErrorCode != 0` → Problemgeräte (Warnung).
- **Netzwerk** — aktiver Adapter, IPv4, Gateway (NetworkInformation/WMI); Ping auf `1.1.1.1` → Internet erreichbar?
- **Ereignisprotokoll** — `EventLogReader` (Log „System", Level 1–2, letzte 7 Tage): Anzahl kritischer Fehler; Kernel-Power ID 41 → Hinweis „unerwartete Abschaltung".

Jeder Check fängt Fehler intern ab und liefert dann ein `CheckResult` mit
Severity `Info`/`Warning` und Text „nicht prüfbar" statt zu werfen.

## Reparaturen (Fix-Aktionen) — mit Sicherheitsregeln

Jede systemverändernde Aktion: **Bestätigungsdialog** + (wo sinnvoll)
**Wiederherstellungspunkt vorher** + **Logging**.

- **Temp/Cache leeren** — `%temp%`, Windows-Temp; optional `cleanmgr`. (unkritisch)
- **Systemdateien reparieren** — `sfc /scannow`, dann `DISM /Online /Cleanup-Image /RestoreHealth`. Ausgabe live in den Fortschritt streamen.
- **Windows-Update reparieren** — Dienste stoppen (`wuauserv`, `bits`, `cryptsvc`), `SoftwareDistribution` + `catroot2` umbenennen, Dienste starten. → Wiederherstellungspunkt + Bestätigung.
- **Datenträger prüfen** — `chkdsk` (zuerst read-only Scan; `/F` nur nach Bestätigung, plant Neustart).
- **DNS-Cache leeren / Netzwerk-Reset** — `ipconfig /flushdns`; optional `netsh winsock reset` (Neustart nötig → Bestätigung).
- **Defender-Schnellscan starten** — `Start-MpScan`/`MpCmdRun`.
- **Autostart-Eintrag deaktivieren** — einzelne Einträge (Registry-Run-Keys / Startup-Ordner) **deaktivieren statt löschen** (reversibel).

**Malware-Tiefenentfernung NICHT selbst bauen** — stattdessen Hinweis/Link zu
bewährten Tools (z. B. Malwarebytes). Wir orchestrieren Bordmittel, erfinden
keine Sicherheitssoftware neu.

## UI / UX

- **Start:** großer Button „Vollständigen Scan starten"; währenddessen Fortschritt mit aktueller Prüfung.
- **Dashboard nach Scan:** Kachel-Raster, je Bereich eine Karte mit Ampelfarbe (grün/gelb/rot), Titel, Kurzbefund. Oben eine **Gesamtampel** + Zähler (x Kritisch, y Warnungen).
- **Karte anklicken** → Detailpanel mit allen Werten des Bereichs; jedes gefundene Problem mit „Beheben"-Button (plus „Alle beheben" oben).
- **Fix-Flow:** Bestätigungsdialog (was passiert, ob Wiederherstellungspunkt angelegt wird) → Fortschritts-Overlay mit Live-Log → Erfolg/Fehler + automatischer Re-Check des Bereichs.
- **Bericht:** „Als HTML exportieren" erzeugt eine Datei im Stil des PowerShell-Berichts (zum Aushändigen).
- **Farben:** Navy `#0d1f3c` (Header), Signalblau als Akzent, Ampel grün/gelb/rot. Segoe UI, großzügige Abstände, keine überladenen Tabellen.

## Packaging / Build

Single-File, self-contained, x64:

```
dotnet publish src/DariaTech.PcDoctor -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

- WPF unterstützt **kein** vollständiges Trimming → self-contained ohne aggressives Trimming (~150 MB). Dafür läuft die `.exe` ohne Runtime-Installation beim Kunden.
- **UAC:** `app.manifest` mit `requireAdministrator` (Checks/Fixes brauchen Adminrechte).
- **Code-Signing:** Build-Schritt mit `signtool` und DariaTech-Zertifikat (gegen SmartScreen-Warnung). Platzhalter im README.
- App-Icon + Versionsinfo setzen.

## Sicherheits-/Verhaltensregeln (wichtig)

- App erfordert Adminrechte; falls nicht vorhanden, sauber zur Elevation auffordern.
- Vor systemverändernden Reparaturen automatisch einen **Systemwiederherstellungspunkt** anlegen.
- **Keine** destruktive Aktion ohne explizite Nutzerbestätigung.
- Alles protokollieren (Serilog); Log einsehbar und Teil des Berichts.
- Reversible Fixes bevorzugen (deaktivieren statt löschen).
- **Niemals** Datenträger formatieren, Partitionen ändern oder Registry-Massenänderungen vornehmen. Nur die hier definierten Fixes.

## Projektstruktur

```
pc-doctor/
├─ CLAUDE.md
├─ README.md
├─ .gitignore
├─ PC-Schnellcheck.ps1          # vorhandener Prototyp als Referenz
└─ src/
   └─ DariaTech.PcDoctor/
      ├─ DariaTech.PcDoctor.csproj
      ├─ app.manifest
      ├─ App.xaml(.cs)          # DI/Hosting/Serilog-Setup (von dir anzulegen)
      ├─ Core/
      │  ├─ Severity.cs
      │  ├─ CheckResult.cs
      │  ├─ ICheck.cs
      │  ├─ IFixAction.cs
      │  └─ DiagnosticEngine.cs
      ├─ Checks/
      │  └─ DiskSpaceCheck.cs    # Beispiel; übrige Checks ergänzen
      ├─ Fixes/
      ├─ UI/{Views,ViewModels}/
      └─ Models/
```

## Build & Run (Entwicklung)

- `dotnet build`
- `dotnet run --project src/DariaTech.PcDoctor`
- Tests: `dotnet test`

## Vorgehen / Meilensteine (inkrementell)

1. **Gerüst:** Solution + WPF-App startet, DI/Hosting/Serilog, leeres Dashboard-Fenster (WPF-UI-Theme).
2. **Core + erste Sichtbarkeit:** Abstraktionen sind vorhanden (siehe `Core/`) — `DiagnosticEngine` an die UI hängen, `DiskSpaceCheck` + `SmartHealthCheck` ausführen, Ergebnisse als Ampel-Karten zeigen.
3. **Checks portieren:** restliche Prüfungen aus der Liste oben nachbauen.
4. **Reparatur-Basis:** `RepairService` + Wiederherstellungspunkt + erste Fixes (Temp leeren, SFC/DISM) mit Live-Fortschritt und Re-Check.
5. **Restliche Fixes.**
6. **HTML-Bericht-Export** (`ReportExporter`).
7. **Packaging:** Single-File-`.exe`, UAC-Manifest prüfen, Icon, Signing-Schritt dokumentieren.
8. **Tests** für Check-/Fix-Logik (xUnit), inkl. Severity-Regeln.

## Konventionen

- Async für alle I/O; `CancellationToken` durchreichen.
- Checks/Fixes UI-frei und einzeln in eigener Datei; ViewModels dünn halten.
- **UI-Texte Deutsch, Code/Bezeichner Englisch.**
- `try/catch` je Check — ein Fehler darf den Gesamtscan nie abbrechen.
