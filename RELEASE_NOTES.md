# DariaTech PC-Doktor 1.0.0

Erste vollständige Fassung. Zwei Werkzeuge in einem Release:

| Download | Für | Installation |
|---|---|---|
| `DariaTech-PC-Doktor.exe` | Windows 10/11 (x64) | keine – vom USB-Stick starten |
| `DariaTech-Mac-Doktor.zip` | macOS | keine – `bash dariatech-mac-check.sh` |

Die `.exe` ist self-contained: Auf dem Kundenrechner muss **keine .NET-Runtime**
installiert sein. Sie fordert beim Start Administratorrechte an (UAC).

---

## Windows: PC-Doktor

**Diagnose** – 25 Prüfungen als Ampel-Dashboard: System & Windows, CPU/RAM,
Speicherplatz und Platzfresser, SMART-Gesundheit inkl. Restlebensdauer/TBW,
Akku, Defender & Sicherheitsstatus, BitLocker, Systemwiederherstellung,
Schadsoftware-Befunde und Befall-Indikatoren, Windows-Updates, Autostart,
installierte Programme, Treiber und Geräte, Netzwerk und Netzwerk-Qualität,
Ereignisprotokoll, Programmabstürze der letzten 30 Tage sowie Updates &
Stabilität. Ein Fehler in einer Prüfung bricht den Scan nie ab.

**Reparaturen** – 30 Aktionen, jede mit Bestätigungsdialog, Live-Protokoll und
automatischem Re-Check des Bereichs: Temp und Browser-Caches leeren, SFC/DISM,
Windows-Update reparieren, chkdsk (schreibgeschützt), DNS/Winsock/Netzwerk,
Defender-Scans und Bedrohungsentfernung, Offlinescan, hosts-Datei zurücksetzen,
Autostart-Einträge reversibel deaktivieren, Bloatware entfernen,
Windows-Suche zurücksetzen, Dienste für Audio/Bluetooth/Drucker/Startmenü,
Systemwiederherstellung einschalten, BitLocker-Schlüssel prüfen.

**Gaming & Stresstest** – Live-Tachos für CPU-/GPU-Temperatur, Last und Lüfter,
Temperaturverlauf, Stresstest mit CPU-, RAM- und GPU-Last, Stopp-Taste und
thermischer Notabschaltung. Liefert das System keine Temperatur, sagt die App
das offen – und warnt vor dem Start, dass die Notabschaltung dann nicht greifen
kann.

**Problemlöser** – Symptom-Assistent: Der Kunde beschreibt das Problem
(„Windows-Suche geht nicht", „PC ist langsam" …), die App führt gezielt die
passenden Prüfungen aus und schlägt die passenden Reparaturen vor.

**Klonen** – 1:1-Kopie eines Datenträgers über ddrescue, mit mehrstufiger
Sicherheitsprüfung.

**Bericht & Verlauf** – Kundenbericht als HTML und PDF mit DariaTech-Branding,
Gesundheits-Score und Vorher/Nachher-Vergleich; Einsatzverlauf portabel neben
der App gespeichert.

**Zugangsschutz (PIN)** – PBKDF2-Hash (600 000 Runden), Sperre nach fünf
Fehlversuchen mit steigender Wartezeit, automatische Sperre nach 30 Minuten
ohne Bedienung.

> **Diese `.exe` ist ohne PIN gebaut.** Das Geheimnis gehört nicht auf GitHub.
> Die geschützte Fassung entsteht auf dem eigenen Rechner:
> `build\publish.ps1 -Pin (Read-Host -AsSecureString "PIN")`

## macOS: Mac-Doktor

Shell-Skript ohne Installation, mit demselben Bericht, denselben Ampelregeln
und demselben Gesundheits-Score wie unter Windows – ein PC- und ein Mac-Bericht
sind damit direkt vergleichbar.

14 Prüfbereiche (System, CPU/Speicher, Speicherplatz, SMART, Akku, FileVault/
Firewall/SIP/Gatekeeper, Schadsoftware-Hinweise, Updates, Startobjekte,
Abstürze und Kernel Panics, Netzwerk, Time Machine, Spotlight, Zwischenspeicher).
Ohne `--fix-…` rein lesend. `--fix-cache` leert die Browser-Caches über beide
macOS-Cache-Zweige und alle Profile – Lesezeichen, Kennwörter und Sitzungen
bleiben erhalten, laufende Browser werden nicht angefasst, und es wird
nachgemessen statt Erfolg behauptet.

```bash
bash dariatech-mac-check.sh --open
```

## Sicherheitsregeln

- Keine systemverändernde Aktion ohne Bestätigung.
- Vor systemnahen Reparaturen ein Wiederherstellungspunkt. Klappt der nicht,
  wird abgebrochen, sofern nicht ausdrücklich zugestimmt wird.
- Reversible Fixes bevorzugt: Autostart wird deaktiviert statt gelöscht,
  Sicherungskopien werden nie überschrieben.
- Klonen: unbekannter Schutzstatus einer Platte gilt als geschützt; zusätzlich
  wird unabhängig geprüft, auf welcher Platte Windows liegt.
- Kein Formatieren, keine Partitionsänderungen, keine Registry-Massenänderungen.
- BitLocker-Wiederherstellungsschlüssel erscheint nur am Bildschirm – nie im
  Protokoll und nie im Kundenbericht.

## Vor dem Kundeneinsatz

**Code-Signatur.** Diese `.exe` ist nicht signiert. Ohne Signatur zeigt Windows
SmartScreen beim ersten Start eine Warnung, und für die Sensorik (Temperaturen,
Lüfter) wird ein Kernel-Treiber geladen, den manche Virenscanner beanstanden.
Vorgehen steht in [`RELEASE.md`](RELEASE.md).

**Prüfsummen.** `SHA256SUMS.txt` liegt bei.
