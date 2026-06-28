# DariaTech PC-Doktor

Native Windows-Desktop-App (.NET 8 / WPF), die den Zustand eines Windows-PCs
als **Ampel-Dashboard** anzeigt und gängige Probleme **per Klick** behebt.
Läuft als portable `.exe` vom USB-Stick, fordert Adminrechte an und exportiert
einen HTML-Bericht zum Aushändigen.

> Vollständige Spezifikation und Architektur: siehe [`CLAUDE.md`](./CLAUDE.md).
> Diese Datei dient Claude Code als Projektkontext.

## Stack

.NET 8 · WPF + WPF-UI (Fluent) · CommunityToolkit.Mvvm · System.Management (WMI) · Serilog

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
