# Release-Leitfaden — DariaTech PC-Doktor

Schritt-für-Schritt zum auslieferbaren, signierten Build.

## 1. Version setzen

In `src/DariaTech.PcDoctor/DariaTech.PcDoctor.csproj` `Version`, `FileVersion`
und `AssemblyVersion` erhöhen (z. B. `0.2.0`).

## 2. Build erzeugen

**Variante A – lokal (Windows, .NET 8 SDK):**

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1
# Ergebnis: artifacts\DariaTech.PcDoctor.exe
```

**Variante B – über GitHub Actions (ohne lokale Toolchain):**

Actions → **Release** → *Run workflow* (oder einen Tag `vX.Y.Z` pushen).
Die fertige `.exe` liegt anschließend als Artefakt **DariaTech-PC-Doktor-win-x64**
am Workflow-Lauf zum Download bereit — ideal zum Testen auf echter Hardware.

## 3. Signieren (wichtig wegen Treiber)

Die App nutzt für die Sensorik (Temperaturen/Lüfter im Gaming-Tab) einen
Kernel-Treiber (über LibreHardwareMonitor). **Unsignierte** Builds lösen daher
eher SmartScreen-/Antivirus-Warnungen aus. Vor der Auslieferung signieren:

```powershell
$env:DARIATECH_PFX_PASSWORD = "<PASSWORT>"
powershell -ExecutionPolicy Bypass -File build\sign.ps1 -Pfx C:\zert\DariaTech-CodeSigning.pfx
```

Zertifikat/Passwort **nie** committen (siehe `.gitignore`).

## 4. Auf echter Hardware verifizieren (Pflicht vor Kundeneinsatz)

Da CI nur Kompilierung + Logik-Tests prüft, muss der erste echte Durchlauf
manuell erfolgen:

- [ ] App startet, fordert sauber Adminrechte an (UAC).
- [ ] **Diagnose:** „Vollständigen Scan starten" → alle Bereiche liefern Werte,
      Ampeln plausibel; HTML- und PDF-Export öffnen korrekt.
- [ ] **Reparaturen:** je eine unkritische Aktion testen (z. B. Temp leeren,
      DNS-Cache leeren); Bestätigung + Live-Log erscheinen.
- [ ] **Gaming-Tab:** „Live-Überwachung" zeigt echte Temperaturen/Lüfter;
      Stresstest startet, **Stopp-Button** und **Thermo-Notabschaltung** greifen.
- [ ] **Verlauf:** „Im Verlauf speichern" legt Eintrag + Bericht an;
      „Bericht öffnen" funktioniert.
- [ ] Auf einem Gerät **ohne** Sensorik/Akku prüfen, dass alles sauber
      degradiert (keine Abstürze, nur Hinweise).

## 4a. Klon-Engine bereitstellen (Tab „Klonen")

Der Klon-Assistent nutzt **GNU ddrescue** als Engine (auch für defekte Platten).
Aus Lizenz-/Signaturgründen ist sie **nicht** in der `.exe` enthalten:

- Einen Windows-Build von `ddrescue.exe` besorgen und in den Ordner **`tools`**
  neben der `.exe` legen (`…\tools\ddrescue.exe`).
- Ohne diese Datei zeigt der Tab den Hinweis „ddrescue.exe fehlt" und das
  Klonen bleibt gesperrt.

Sicherheitsverhalten des Assistenten (zur Erinnerung): Quelle/Ziel werden mit
Modell/Seriennummer/Größe/SMART angezeigt; die System-/Startplatte ist als Ziel
**gesperrt**; zu kleines Ziel und identische Auswahl werden blockiert; der Start
erfordert die getippte Bestätigung **KLONEN** und einen Bestätigungsdialog.
ddrescue legt je Lauf ein Mapfile/Protokoll unter `DariaTech-Klonlogs` ab.

## 5. Übergabe

`artifacts\DariaTech.PcDoctor.exe` (signiert) auf den USB-Stick kopieren.
Der Kundenverlauf wird portabel im Ordner `DariaTech-Verlauf` neben der `.exe`
abgelegt und wandert so mit dem Stick mit.
