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
  Bestätigung und Live-Log; Fixes: Temp leeren, SFC/DISM, DNS-Cache leeren,
  Defender-Schnellscan. Nach einem Fix wird der Bereich neu geprüft.
- **HTML-Bericht:** `ReportExporter` im Stil des PowerShell-Berichts.
- **Tests:** xUnit für Severity-Regeln, Engine-Robustheit und Bericht.

> Hinweis: WPF/`net8.0-windows` lässt sich nur unter **Windows** bauen.

## Entwicklung

```bash
dotnet restore
dotnet build
dotnet run --project src/DariaTech.PcDoctor
dotnet test
```

## Release-Build (portable Single-File-.exe)

```bash
dotnet publish src/DariaTech.PcDoctor -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Ergebnis: eine eigenständige `.exe` (~150 MB), läuft ohne .NET-Installation
beim Kunden. WPF erlaubt kein vollständiges Trimming — daher self-contained.

## Code-Signing (gegen SmartScreen-Warnung)

```bash
signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 ^
  /f "DariaTech-CodeSigning.pfx" /p <PASSWORT> ^
  "bin\Release\net8.0-windows\win-x64\publish\DariaTech.PcDoctor.exe"
```

Zertifikat und Passwort nicht ins Repo committen (siehe `.gitignore`).

## Sicherheit

Die App erfordert Adminrechte. Vor systemverändernden Reparaturen wird
automatisch ein Systemwiederherstellungspunkt angelegt; keine destruktive
Aktion ohne Bestätigung. Details in `CLAUDE.md` → „Sicherheits-/Verhaltensregeln".
