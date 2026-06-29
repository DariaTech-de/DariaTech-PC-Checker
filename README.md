# DariaTech PC-Doktor

Native Windows-Desktop-App (.NET 8 / WPF), die den Zustand eines Windows-PCs
als **Ampel-Dashboard** anzeigt und gängige Probleme **per Klick** behebt.
Läuft als portable `.exe` vom USB-Stick, fordert Adminrechte an und exportiert
einen HTML-Bericht zum Aushändigen.

> Vollständige Spezifikation und Architektur: siehe [`CLAUDE.md`](./CLAUDE.md).
> Diese Datei dient Claude Code als Projektkontext.

## Stack

.NET 8 · WPF + WPF-UI (Fluent) · CommunityToolkit.Mvvm · System.Management (WMI) · Serilog

## Umsetzungsstand

Bereits umgesetzt:

- **Gerüst:** WPF-App mit DI/Hosting, Serilog-Logging (Datei) und Fluent-Theme.
- **Diagnose:** alle Prüfungen aus `PC-Schnellcheck.ps1` nativ in C# unter
  `Checks/` (System, CPU/RAM, Speicherplatz, SMART, Akku, Defender, Updates,
  Autostart, Treiber, Netzwerk, Ereignisprotokoll). Die langsame Update-Suche
  ist per „Schnellmodus" überspringbar.
- **Dashboard:** Ampel-Kacheln je Bereich, Gesamtampel + Zähler, Detailpanel.
- **Reparaturen:** `RepairService` mit automatischem Wiederherstellungspunkt,
  Bestätigung und Live-Log. Fixes: Temp leeren, SFC/DISM, Windows-Update
  reparieren, Datenträger prüfen (chkdsk, schreibgeschützt), DNS-Cache leeren,
  Winsock-Reset, Defender-Schnellscan und reversibles Deaktivieren einzelner
  Autostart-Einträge. Nach einem Fix wird der Bereich neu geprüft.
- **Gaming & Stresstest (eigener Tab):** Live-Tachos (CPU-/GPU-Temp, Last,
  Lüfter) und Temperaturverlauf über LibreHardwareMonitor; Stresstest mit
  Abschlussbericht (Throttling/Stabilität), **Stopp-Button** und automatischer
  **Thermo-Notabschaltung**.
- **Kundenbericht (Übergabe):** DariaTech-Branding (Logo, Herausgeberdaten),
  Kundendaten-Kopf, Gesundheits-Score; Export als **HTML und PDF**.
- **Field-Tech:** SMART-Detailwerte (Restlebensdauer/TBW/Temperatur),
  Akku-Report (`powercfg`), Internet-Speedtest, **Kundenverlauf** (Tab „Verlauf",
  portabel neben der App gespeichert).
- **Packaging:** App-Icon, UAC-Manifest, Versions-/Herstellerinfo, Build-Skripte
  (`build/publish.ps1`, `build/sign.ps1`) und ein **Release-Workflow**
  (lädt die fertige `.exe` als Artefakt hoch). Siehe [`RELEASE.md`](./RELEASE.md).
- **Tests:** xUnit für Severity-/Score-Regeln, Engine-Robustheit, Bericht und
  Stresstest-Auswertung.

> **Hinweise:** WPF/`net8.0-windows` lässt sich nur unter **Windows** bauen.
> Für die Gaming-Sensorik wird ein Kernel-Treiber geladen (LibreHardwareMonitor);
> unsignierte Builds können daher SmartScreen-/Antivirus-Warnungen auslösen –
> vor Auslieferung **signieren** (siehe `RELEASE.md`).

## Entwicklung

```bash
dotnet restore
dotnet build
dotnet run --project src/DariaTech.PcDoctor
dotnet test
```

## Release-Build (portable Single-File-.exe)

Am einfachsten über das Skript (legt die `.exe` unter `artifacts\` ab):

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1
```

Oder direkt:

```bash
dotnet publish src/DariaTech.PcDoctor -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Ergebnis: eine eigenständige `.exe` (~150 MB), läuft ohne .NET-Installation
beim Kunden. WPF erlaubt kein vollständiges Trimming — daher self-contained.

## Code-Signing (gegen SmartScreen-Warnung)

```powershell
$env:DARIATECH_PFX_PASSWORD = "<PASSWORT>"
powershell -ExecutionPolicy Bypass -File build\sign.ps1 -Pfx C:\zert\DariaTech-CodeSigning.pfx
```

Intern ruft das Skript `signtool` auf:

```bash
signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 ^
  /f "DariaTech-CodeSigning.pfx" /p <PASSWORT> ^
  "artifacts\DariaTech.PcDoctor.exe"
```

Zertifikat und Passwort nicht ins Repo committen (siehe `.gitignore`).

## Sicherheit

Die App erfordert Adminrechte. Vor systemverändernden Reparaturen wird
automatisch ein Systemwiederherstellungspunkt angelegt; keine destruktive
Aktion ohne Bestätigung. Details in `CLAUDE.md` → „Sicherheits-/Verhaltensregeln".
