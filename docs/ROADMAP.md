# Roadmap — DariaTech PC-Doktor

Aufbauend auf dem MVP (Diagnose, Reparaturen, Ampel-Dashboard, HTML-Bericht).
Vereinbarte Ausbaurichtung:

## A. Hardware-Sensorik (Fundament)
- Integration **LibreHardwareMonitorLib** (CPU-/GPU-Temps, Lüfter-RPM, Takt,
  Last, Spannung, Leistung) über eine gekapselte `ISensorService`-Schicht.
- Lädt einen Kernel-Treiber (WinRing0) → Adminrechte (vorhanden) + **Code-Signing
  wird Pflicht**. Saubere Degradation, falls der Treiber nicht lädt.

## B. Stresstest + Live-Monitoring (Gaming-PCs)
- Stresstest CPU/RAM (optional GPU) mit sekündlichem Sampling.
- Abschlussbericht: min/Ø/max Temperaturen, max Lüfter-RPM, **Throttling-Erkennung**
  (Takt fällt unter Last / Temp am Limit), **Stabilität** (Fehler/WHEA).

## C. Modernes Gaming-UI
- Tacho-/Gauge-Anzeigen + Live-Liniendiagramme (Temp/Takt/Last über Zeit) via
  **LiveCharts2**. Eigener Monitoring-/Stresstest-Bereich.

## D. Field-Tech-Features (Vor-Ort-Einsatz)
- Kundendaten-Kopf + **PDF-Bericht** + Unterschriftsfeld, „Gesundheits-Score".
- Hardware-Inventar (Seriennummern), **SMART-Detailwerte** (TBW, Betriebsstunden,
  Restlebensdauer), **Akku-Report** (`powercfg`), Speedtest, Verlauf pro Kunde.

## E. Release-Reife
- App-Icon, **Code-Signing** (wegen Treiber zwingend), erster verifizierter
  Durchlauf auf echter Windows-Hardware.

> Hinweis: Sensor-, Stresstest- und UI-Verhalten lässt sich nur auf echter
> Windows-Hardware verifizieren (CI prüft nur Kompilierung + Unit-Tests).
