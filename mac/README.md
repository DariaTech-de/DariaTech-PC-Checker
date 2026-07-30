# DariaTech Mac-Doktor

Schnellprüfung für macOS mit HTML-Bericht zum Aushändigen – im gleichen Design
wie der Windows-Bericht des PC-Doktors.

Das ist **kein Port der Windows-App**, sondern ein eigenes, kleines Werkzeug.
Grund: Die WPF-Oberfläche, WMI, Registry, BitLocker, Defender und der
Systemwiederherstellungspunkt gibt es auf dem Mac schlicht nicht. Was sich
sinnvoll übertragen lässt, ist übertragen worden – die Bewertungsregeln, der
Gesundheits-Score und das Berichtslayout sind identisch.

## Aufruf

Kein Installieren, kein Nachladen. Skript auf den USB-Stick kopieren, Terminal
öffnen, ausführen:

```bash
bash /Volumes/USB/mac/dariatech-mac-check.sh
```

| Option | Wirkung |
|---|---|
| `--out ORDNER` | Zielordner für den Bericht (Standard: `~/Desktop`) |
| `--no-report` | nur Ausgabe im Terminal, keine HTML-Datei |
| `--open` | Bericht danach im Browser öffnen |
| `--skip-updates` | Update-Suche überspringen (die dauert am längsten) |
| `--fix-cache` | Browser-Zwischenspeicher leeren |
| `--fix-dns` | DNS-Zwischenspeicher leeren (fragt nach dem Kennwort) |
| `--yes` | Rückfragen bei Reparaturen überspringen |

Rückgabewert: `0` = alles in Ordnung, `1` = Warnungen, `2` = kritische Befunde.

Ohne `--fix-…` ist der Durchlauf **rein lesend**. Es wird nichts verändert.

## Was geprüft wird

| Bereich | Quelle | Ampelregel |
|---|---|---|
| System & macOS | `sw_vers`, `system_profiler`, `kern.boottime` | Laufzeit > 14 Tage → gelb |
| Prozessor & Speicher | `vm.loadavg`, `memory_pressure`, `vm.swapusage` | Last > 90 %, frei < 10 %, Swap > 4 GB → gelb |
| Speicherplatz | `df -kl` | < 15 % frei → gelb, < 10 % → rot |
| SMART | `diskutil info` | alles außer „Verified“ → rot |
| Akku | `system_profiler SPPowerDataType` | Zustand ≠ Normal, > 1000 Zyklen, < 60 % Kapazität → gelb |
| Sicherheit | `fdesetup`, `socketfilterfw`, `csrutil`, `spctl` | Schalter aus → gelb |
| Schadsoftware | XProtect-Version, `profiles -P`, `kmutil showloaded` | Konfigurationsprofile vorhanden → gelb |
| Software-Updates | `softwareupdate -l` | ausstehende Updates → gelb |
| Automatischer Start | LaunchAgents/LaunchDaemons | > 15 Einträge → gelb |
| Abstürze | `~/Library/Logs/DiagnosticReports` (30 Tage) | ≥ 3 → gelb, ≥ 10 → rot; 1 Kernel Panic → gelb, 3 → rot |
| Netzwerk | `route`, `ipconfig`, `scutil --dns`, Ping 1.1.1.1 | keine Route / kein Ping → gelb |
| Datensicherung | `tmutil latestbackup` | keine Sicherung oder > 30 Tage → gelb, > 7 Tage → Hinweis |
| Spotlight-Suche | `mdutil -s /` | Index abgeschaltet → gelb |
| Zwischenspeicher | Browser-Cache-Ordner, `~/Library/Caches`, Papierkorb | > 3 GB Browser-Cache → gelb |

Die Schwellwerte sind absichtlich dieselben wie in der Windows-App, damit ein
Bericht vom Mac und einer vom PC vergleichbar sind.

## Browser-Zwischenspeicher (`--fix-cache`)

Der ursprüngliche Fehler war, dass der Chrome-Cache auf dem Mac stehen blieb.
Ursache: Unter Windows liegt alles in einem Ordner, auf dem Mac in **zwei**
getrennten Zweigen:

```
~/Library/Caches/Google/Chrome/<Profil>/Cache, Code Cache, …
~/Library/Application Support/Google/Chrome/<Profil>/GPUCache, DawnCache, …
~/Library/Application Support/Google/Chrome/ShaderCache, GrShaderCache
```

Beide Zweige werden jetzt durchsucht, über **alle** Profile hinweg. Unterstützt
sind Chrome, Edge, Brave, Vivaldi, Opera, Firefox und Safari.

Regeln beim Löschen:

* Es wird nur der **Inhalt** bekannter Cache-Ordner entfernt, nie ein Ordner
  selbst und nie etwas außerhalb der Liste.
* Jeder Pfad muss zusätzlich `cleanup_is_safe_target()` bestehen – absolut,
  unterhalb von `$HOME` bzw. `/Library`, mindestens vier Ebenen tief, mit
  Cache-Bezug im Namen. Dokumente, Bilder, Schreibtisch und Schlüsselbund sind
  ausdrücklich gesperrt.
* **Nicht angefasst** werden `Local Storage`, `IndexedDB`, `Cookies`,
  `Login Data`, `Sessions`, Lesezeichen und Erweiterungs-Einstellungen. Der
  Kunde bleibt überall angemeldet.
* Läuft der Browser noch, wird **nichts** gelöscht – sonst schreibt er die
  Dateien sofort neu und wir würden fälschlich Erfolg melden.
* Nach dem Löschen wird nachgemessen. Bleiben mehr als 10 MB übrig, meldet das
  Werkzeug „nur teilweise geleert“ statt „erledigt“.

## Was der Mac-Doktor nicht tut

* **Keine Malware-Tiefenentfernung.** macOS hat kein Virenschutz-Programm, das
  sich fernsteuern ließe. Geprüft werden XProtect-Version, Konfigurationsprofile
  und fremde Kernel-Erweiterungen – bei konkretem Verdacht gehört Malwarebytes
  für Mac dazu.
* **Kein Systemwiederherstellungspunkt.** Das Gegenstück heißt Time Machine und
  muss vorher eingerichtet sein; genau deshalb wird das Sicherungsalter geprüft.
* **Kein Stresstest.** Für Lasttests auf dem Mac gibt es kein Bordmittel, mit
  dem sich zuverlässige Temperatur- und Taktwerte auslesen ließen. Lieber gar
  keine Zahl als eine falsche.
* **Keine Reparatur ohne Nachfrage.** Ohne `--fix-…` verändert das Werkzeug
  nichts.

## Selbsttest

Die Bewertungsregeln, der Berichtsaufbau und die Sicherheitsschranke der
Aufräum-Funktionen sind ohne Mac testbar und laufen bei jedem Push in der CI:

```bash
bash mac/selftest.sh
```

Der Test legt unter anderem einen echten Chrome-Ordnerbaum an, lässt ihn
aufräumen und prüft danach, dass die Cache-Dateien weg und die Nutzdaten noch da
sind.

## Aufbau

```
mac/
├─ dariatech-mac-check.sh   # Einstiegspunkt, Ablauf, Reparaturen
├─ selftest.sh              # läuft auch ohne macOS
├─ README.md
└─ lib/
   ├─ rules.sh              # Ampelregeln (reine Funktionen)
   ├─ report.sh             # Ergebnissammlung, Terminal- und HTML-Ausgabe
   ├─ cleanup.sh            # Cache-Katalog + Sicherheitsschranke
   └─ collect.sh            # macOS-Datenerhebung (rein lesend)
```
