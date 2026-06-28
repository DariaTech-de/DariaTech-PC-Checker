#Requires -Version 5.1
<#
.SYNOPSIS
  DariaTech PC-Schnellcheck - schnelle Zustandsanalyse fuer Windows-PCs.

.DESCRIPTION
  Liest Hardware, Arbeitsspeicher, Datentraeger-Gesundheit (SMART), Windows-
  Status, Sicherheit, Treiber und das Ereignisprotokoll aus und gibt eine
  farbcodierte Uebersicht plus einen HTML-Bericht aus, den du dem Kunden
  aushaendigen kannst.

  Am besten ALS ADMINISTRATOR ausfuehren (sonst fehlen ein paar Werte).

  Start (PowerShell als Admin):
      powershell -ExecutionPolicy Bypass -File .\PC-Schnellcheck.ps1

  Parameter:
      -BerichtPfad "C:\Pfad"   Zielordner fuer den HTML-Bericht (Standard: Desktop)
      -KeinBericht             Nur Konsolenausgabe, kein HTML-Bericht
      -SchnellModus            Ueberspringt die langsame Windows-Update-Pruefung

.NOTES
  Version 1.0  |  DariaTech IT-Systemhaus
#>

[CmdletBinding()]
param(
    [string]$BerichtPfad = "$env:USERPROFILE\Desktop",
    [switch]$KeinBericht,
    [switch]$SchnellModus
)

$ErrorActionPreference = 'Continue'

# ============================================================
#  Hilfsfunktionen
# ============================================================
$Script:Report  = [ordered]@{}                                   # Abschnitt -> Eintraege
$Script:Befunde = New-Object System.Collections.Generic.List[object]

function Add-Wert {
    param(
        [string]$Abschnitt,
        [string]$Label,
        [string]$Wert,
        [ValidateSet('ok','warn','crit','info')]$Stufe = 'info'
    )
    if (-not $Script:Report.Contains($Abschnitt)) {
        $Script:Report[$Abschnitt] = New-Object System.Collections.Generic.List[object]
    }
    $Script:Report[$Abschnitt].Add([pscustomobject]@{ Label=$Label; Wert=$Wert; Stufe=$Stufe })

    $farbe = switch ($Stufe) { 'ok' {'Green'} 'warn' {'Yellow'} 'crit' {'Red'} default {'Gray'} }
    Write-Host ("  {0,-26} {1}" -f $Label, $Wert) -ForegroundColor $farbe
}

function Add-Befund {
    param([ValidateSet('warn','crit')]$Stufe, [string]$Text)
    $Script:Befunde.Add([pscustomobject]@{ Stufe=$Stufe; Text=$Text })
}

function Write-Abschnitt {
    param([string]$Titel)
    Write-Host ""
    Write-Host ("-" * 62) -ForegroundColor DarkGray
    Write-Host "  $Titel" -ForegroundColor Cyan
    Write-Host ("-" * 62) -ForegroundColor DarkGray
}

function Format-GB { param([double]$Bytes) "{0:N1} GB" -f ($Bytes / 1GB) }

function HtmlEnc {
    param([string]$t)
    if ($null -eq $t) { return "" }
    $t -replace '&','&amp;' -replace '<','&lt;' -replace '>','&gt;'
}

# ============================================================
#  Start
# ============================================================
Clear-Host
Write-Host ""
Write-Host "  ==============================================" -ForegroundColor Blue
Write-Host "   DariaTech - PC-Schnellcheck" -ForegroundColor White
Write-Host "  ==============================================" -ForegroundColor Blue

$istAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $istAdmin) {
    Write-Host "`n  [!] Hinweis: laeuft OHNE Administratorrechte - einige Werte fehlen evtl.`n" -ForegroundColor Yellow
}

# ============================================================
#  System & Betriebssystem
# ============================================================
Write-Abschnitt "System & Betriebssystem"
try {
    $cs  = Get-CimInstance Win32_ComputerSystem
    $os  = Get-CimInstance Win32_OperatingSystem
    $bios= Get-CimInstance Win32_BIOS

    Add-Wert "System & Betriebssystem" "Geraet"          ("{0} {1}" -f $cs.Manufacturer, $cs.Model)
    Add-Wert "System & Betriebssystem" "Seriennummer"    $bios.SerialNumber
    Add-Wert "System & Betriebssystem" "Computername"    $env:COMPUTERNAME
    Add-Wert "System & Betriebssystem" "Windows"         ("{0} (Build {1})" -f $os.Caption, $os.BuildNumber)
    Add-Wert "System & Betriebssystem" "Installiert am"  ($os.InstallDate.ToString('dd.MM.yyyy'))
    Add-Wert "System & Betriebssystem" "BIOS/UEFI"       ("{0}  {1}" -f $bios.Manufacturer, $bios.SMBIOSBIOSVersion)

    $uptime = (Get-Date) - $os.LastBootUpTime
    $upTxt  = "{0} Tage, {1} Std" -f $uptime.Days, $uptime.Hours
    if ($uptime.TotalDays -gt 14) {
        Add-Wert "System & Betriebssystem" "Letzter Neustart" "$upTxt (lange her)" 'warn'
        Add-Befund 'warn' "Seit $($uptime.Days) Tagen kein Neustart - Neustart empfohlen."
    } else {
        Add-Wert "System & Betriebssystem" "Letzter Neustart" $upTxt 'ok'
    }
} catch { Add-Wert "System & Betriebssystem" "Fehler" $_.Exception.Message 'warn' }

# ============================================================
#  Prozessor & Arbeitsspeicher
# ============================================================
Write-Abschnitt "Prozessor & Arbeitsspeicher"
try {
    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
    Add-Wert "Prozessor & Arbeitsspeicher" "CPU"      $cpu.Name.Trim()
    Add-Wert "Prozessor & Arbeitsspeicher" "Kerne"    ("{0} Kerne / {1} Threads" -f $cpu.NumberOfCores, $cpu.NumberOfLogicalProcessors)

    $last = $cpu.LoadPercentage
    if ($last -ne $null) {
        $st = if ($last -gt 90) {'warn'} else {'ok'}
        Add-Wert "Prozessor & Arbeitsspeicher" "CPU-Auslastung" "$last %" $st
    }

    $totalKB = $os.TotalVisibleMemorySize
    $freeKB  = $os.FreePhysicalMemory
    $usedPct = [math]::Round((($totalKB - $freeKB) / $totalKB) * 100, 0)
    $ramTxt  = "{0:N1} GB gesamt, {1} % belegt" -f ($totalKB/1MB), $usedPct
    if ($usedPct -gt 90) {
        Add-Wert "Prozessor & Arbeitsspeicher" "Arbeitsspeicher" $ramTxt 'warn'
        Add-Befund 'warn' "Arbeitsspeicher zu $usedPct % belegt - moeglicher Engpass."
    } else {
        Add-Wert "Prozessor & Arbeitsspeicher" "Arbeitsspeicher" $ramTxt 'ok'
    }

    $riegel = Get-CimInstance Win32_PhysicalMemory
    $bestueckt = ($riegel | Measure-Object).Count
    Add-Wert "Prozessor & Arbeitsspeicher" "RAM-Module" "$bestueckt Riegel verbaut"
} catch { Add-Wert "Prozessor & Arbeitsspeicher" "Fehler" $_.Exception.Message 'warn' }

# ============================================================
#  Grafik
# ============================================================
Write-Abschnitt "Grafik"
try {
    Get-CimInstance Win32_VideoController | ForEach-Object {
        Add-Wert "Grafik" "GPU" $_.Name
    }
} catch { Add-Wert "Grafik" "Fehler" $_.Exception.Message 'warn' }

# ============================================================
#  Datentraeger - Speicherplatz
# ============================================================
Write-Abschnitt "Datentraeger - Speicherplatz"
try {
    Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | ForEach-Object {
        $freiPct = if ($_.Size -gt 0) { [math]::Round(($_.FreeSpace / $_.Size) * 100, 0) } else { 0 }
        $txt = "{0} frei von {1}  ({2} %)" -f (Format-GB $_.FreeSpace), (Format-GB $_.Size), $freiPct
        if ($freiPct -lt 10) {
            Add-Wert "Datentraeger - Speicherplatz" "Laufwerk $($_.DeviceID)" $txt 'crit'
            Add-Befund 'crit' "Laufwerk $($_.DeviceID) nur noch $freiPct % frei - kritisch."
        } elseif ($freiPct -lt 15) {
            Add-Wert "Datentraeger - Speicherplatz" "Laufwerk $($_.DeviceID)" $txt 'warn'
            Add-Befund 'warn' "Laufwerk $($_.DeviceID) wird knapp ($freiPct % frei)."
        } else {
            Add-Wert "Datentraeger - Speicherplatz" "Laufwerk $($_.DeviceID)" $txt 'ok'
        }
    }
} catch { Add-Wert "Datentraeger - Speicherplatz" "Fehler" $_.Exception.Message 'warn' }

# ============================================================
#  Datentraeger - Gesundheit (SMART)
# ============================================================
Write-Abschnitt "Datentraeger - Gesundheit (SMART)"
try {
    Get-PhysicalDisk | ForEach-Object {
        $typ  = $_.MediaType
        $health = $_.HealthStatus
        $txt = "{0}  ({1}, {2})" -f $_.FriendlyName, $typ, (Format-GB $_.Size)
        switch ($health) {
            'Healthy' { Add-Wert "Datentraeger - Gesundheit (SMART)" "Status: OK" $txt 'ok' }
            'Warning' {
                Add-Wert "Datentraeger - Gesundheit (SMART)" "Status: WARNUNG" $txt 'warn'
                Add-Befund 'warn' "Datentraeger '$($_.FriendlyName)' meldet SMART-Warnung - Backup pruefen."
            }
            default {
                Add-Wert "Datentraeger - Gesundheit (SMART)" "Status: $health" $txt 'crit'
                Add-Befund 'crit' "Datentraeger '$($_.FriendlyName)': $health - moeglicher Defekt, sofort Backup!"
            }
        }
    }
} catch { Add-Wert "Datentraeger - Gesundheit (SMART)" "Hinweis" "Get-PhysicalDisk nicht verfuegbar" 'info' }

# Zusaetzlich klassische SMART-Ausfallvorhersage (auch fuer aeltere HDDs)
try {
    $predict = Get-CimInstance -Namespace root\wmi -Class MSStorageDriver_FailurePredictStatus -ErrorAction Stop
    foreach ($p in $predict) {
        if ($p.PredictFailure) {
            Add-Wert "Datentraeger - Gesundheit (SMART)" "Ausfallvorhersage" "FAIL ($($p.InstanceName))" 'crit'
            Add-Befund 'crit' "SMART sagt baldigen Ausfall voraus - Datentraeger umgehend ersetzen!"
        }
    }
} catch { } # Namespace nicht auf jedem System vorhanden - kein Fehler

# ============================================================
#  Akku (nur bei Notebooks)
# ============================================================
$akku = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue
if ($akku) {
    Write-Abschnitt "Akku"
    try {
        $design = (Get-CimInstance -Namespace root\wmi -Class BatteryStaticData -ErrorAction Stop).DesignedCapacity
        $full   = (Get-CimInstance -Namespace root\wmi -Class BatteryFullChargedCapacity -ErrorAction Stop).FullChargedCapacity
        if ($design -gt 0) {
            $verschleiss = [math]::Round((1 - ($full / $design)) * 100, 0)
            $txt = "$verschleiss % Verschleiss ($full / $design mWh)"
            if ($verschleiss -gt 40) {
                Add-Wert "Akku" "Akku-Zustand" $txt 'warn'
                Add-Befund 'warn' "Akku zu $verschleiss % verschlissen - Tausch koennte sinnvoll sein."
            } else {
                Add-Wert "Akku" "Akku-Zustand" $txt 'ok'
            }
        }
    } catch {
        Add-Wert "Akku" "Ladestand" "$($akku.EstimatedChargeRemaining) %" 'info'
    }
}

# ============================================================
#  Windows-Sicherheit (Defender)
# ============================================================
Write-Abschnitt "Windows-Sicherheit"
try {
    $mp = Get-MpComputerStatus -ErrorAction Stop
    $rt = if ($mp.RealTimeProtectionEnabled) {'aktiv'} else {'AUS'}
    if ($mp.RealTimeProtectionEnabled) {
        Add-Wert "Windows-Sicherheit" "Echtzeitschutz" $rt 'ok'
    } else {
        Add-Wert "Windows-Sicherheit" "Echtzeitschutz" $rt 'crit'
        Add-Befund 'crit' "Defender-Echtzeitschutz ist deaktiviert."
    }
    $alter = ((Get-Date) - $mp.AntivirusSignatureLastUpdated).Days
    if ($alter -gt 7) {
        Add-Wert "Windows-Sicherheit" "Signaturen" "$alter Tage alt" 'warn'
        Add-Befund 'warn' "Virensignaturen $alter Tage alt - aktualisieren."
    } else {
        Add-Wert "Windows-Sicherheit" "Signaturen" "aktuell ($alter Tage)" 'ok'
    }
} catch {
    Add-Wert "Windows-Sicherheit" "Hinweis" "Defender-Status nicht abrufbar (evtl. Drittanbieter-AV)" 'info'
}

# ============================================================
#  Windows-Updates (langsam - mit -SchnellModus ueberspringbar)
# ============================================================
Write-Abschnitt "Windows-Updates"
if ($SchnellModus) {
    Add-Wert "Windows-Updates" "Pruefung" "uebersprungen (-SchnellModus)" 'info'
} else {
    try {
        Write-Host "  ... pruefe ausstehende Updates (kann etwas dauern)" -ForegroundColor DarkGray
        $session  = New-Object -ComObject Microsoft.Update.Session
        $searcher = $session.CreateUpdateSearcher()
        $res      = $searcher.Search("IsInstalled=0 and Type='Software'")
        $anzahl   = $res.Updates.Count
        if ($anzahl -gt 0) {
            Add-Wert "Windows-Updates" "Ausstehend" "$anzahl Update(s)" 'warn'
            Add-Befund 'warn' "$anzahl Windows-Update(s) ausstehend - installieren."
        } else {
            Add-Wert "Windows-Updates" "Status" "auf dem neuesten Stand" 'ok'
        }
    } catch {
        Add-Wert "Windows-Updates" "Hinweis" "Update-Pruefung nicht moeglich (offline?)" 'info'
    }
}

# ============================================================
#  Autostart-Programme
# ============================================================
Write-Abschnitt "Autostart-Programme"
try {
    $auto = Get-CimInstance Win32_StartupCommand
    $anz  = ($auto | Measure-Object).Count
    Add-Wert "Autostart-Programme" "Anzahl" "$anz Eintraege"
    $auto | Select-Object -First 8 | ForEach-Object {
        Add-Wert "Autostart-Programme" " - $($_.Name)" $_.Location
    }
    if ($anz -gt 15) {
        Add-Befund 'warn' "$anz Autostart-Eintraege - viele davon bremsen den Start."
    }
} catch { Add-Wert "Autostart-Programme" "Fehler" $_.Exception.Message 'warn' }

# ============================================================
#  Problemgeraete / Treiber
# ============================================================
Write-Abschnitt "Treiber & Geraete"
try {
    $probleme = Get-PnpDevice -PresentOnly -ErrorAction Stop | Where-Object { $_.Status -ne 'OK' }
    if ($probleme) {
        foreach ($g in $probleme) {
            Add-Wert "Treiber & Geraete" "Problem" $g.FriendlyName 'warn'
        }
        Add-Befund 'warn' "$(($probleme|Measure-Object).Count) Geraet(e) mit Treiberproblem im Geraete-Manager."
    } else {
        Add-Wert "Treiber & Geraete" "Status" "alle Geraete in Ordnung" 'ok'
    }
} catch { Add-Wert "Treiber & Geraete" "Fehler" $_.Exception.Message 'warn' }

# ============================================================
#  Netzwerk
# ============================================================
Write-Abschnitt "Netzwerk"
try {
    $cfg = Get-NetIPConfiguration -ErrorAction Stop | Where-Object { $_.IPv4DefaultGateway } | Select-Object -First 1
    if ($cfg) {
        Add-Wert "Netzwerk" "Adapter"  $cfg.InterfaceAlias
        Add-Wert "Netzwerk" "IPv4"     $cfg.IPv4Address.IPAddress
        Add-Wert "Netzwerk" "Gateway"  $cfg.IPv4DefaultGateway.NextHop
    }
    $online = Test-Connection -ComputerName 1.1.1.1 -Count 1 -Quiet -ErrorAction SilentlyContinue
    if ($online) { Add-Wert "Netzwerk" "Internet" "erreichbar" 'ok' }
    else         { Add-Wert "Netzwerk" "Internet" "nicht erreichbar" 'warn' }
} catch { Add-Wert "Netzwerk" "Fehler" $_.Exception.Message 'warn' }

# ============================================================
#  Ereignisprotokoll - kritische Fehler (letzte 7 Tage)
# ============================================================
Write-Abschnitt "Ereignisprotokoll (letzte 7 Tage)"
try {
    $fehler = Get-WinEvent -FilterHashtable @{
        LogName   = 'System'
        Level     = 1,2
        StartTime = (Get-Date).AddDays(-7)
    } -MaxEvents 200 -ErrorAction SilentlyContinue

    if ($fehler) {
        $anz = ($fehler | Measure-Object).Count
        Add-Wert "Ereignisprotokoll (letzte 7 Tage)" "Kritische Fehler" "$anz Eintraege"
        $fehler | Group-Object ProviderName | Sort-Object Count -Descending |
            Select-Object -First 5 | ForEach-Object {
                Add-Wert "Ereignisprotokoll (letzte 7 Tage)" " - $($_.Name)" "$($_.Count)x"
            }
        # Unerwartete Abschaltungen (Kernel-Power 41)
        $power = $fehler | Where-Object { $_.Id -eq 41 }
        if ($power) {
            Add-Befund 'warn' "$(($power|Measure-Object).Count)x unerwartete Abschaltung (Kernel-Power 41) - Netzteil/Treiber pruefen."
        }
        if ($anz -gt 20) {
            Add-Befund 'warn' "Auffaellig viele Systemfehler ($anz in 7 Tagen)."
        }
    } else {
        Add-Wert "Ereignisprotokoll (letzte 7 Tage)" "Status" "keine kritischen Fehler" 'ok'
    }
} catch { Add-Wert "Ereignisprotokoll (letzte 7 Tage)" "Hinweis" "Protokoll nicht lesbar" 'info' }

# ============================================================
#  Zusammenfassung
# ============================================================
Write-Host ""
Write-Host ("=" * 62) -ForegroundColor Blue
Write-Host "  ZUSAMMENFASSUNG" -ForegroundColor White
Write-Host ("=" * 62) -ForegroundColor Blue

$krit = @($Script:Befunde | Where-Object Stufe -eq 'crit')
$warn = @($Script:Befunde | Where-Object Stufe -eq 'warn')

if ($krit.Count -eq 0 -and $warn.Count -eq 0) {
    Write-Host "`n  [OK] Keine Auffaelligkeiten gefunden - System sieht gesund aus.`n" -ForegroundColor Green
} else {
    if ($krit.Count -gt 0) {
        Write-Host "`n  KRITISCH:" -ForegroundColor Red
        $krit | ForEach-Object { Write-Host "   [!] $($_.Text)" -ForegroundColor Red }
    }
    if ($warn.Count -gt 0) {
        Write-Host "`n  WARNUNGEN:" -ForegroundColor Yellow
        $warn | ForEach-Object { Write-Host "   [*] $($_.Text)" -ForegroundColor Yellow }
    }
    Write-Host ""
}

# ============================================================
#  HTML-Bericht
# ============================================================
if (-not $KeinBericht) {
    try {
        if (-not (Test-Path $BerichtPfad)) { New-Item -ItemType Directory -Path $BerichtPfad -Force | Out-Null }
        $datei = Join-Path $BerichtPfad ("PC-Schnellcheck_{0}_{1}.html" -f $env:COMPUTERNAME, (Get-Date -Format 'yyyy-MM-dd_HHmm'))

        # Befunde-Block
        $befundHtml = ""
        if ($krit.Count -eq 0 -and $warn.Count -eq 0) {
            $befundHtml = "<div class='ampel ok'>Keine Auffaelligkeiten gefunden - System sieht gesund aus.</div>"
        } else {
            foreach ($b in $krit) { $befundHtml += "<div class='ampel crit'>$(HtmlEnc $b.Text)</div>" }
            foreach ($b in $warn) { $befundHtml += "<div class='ampel warn'>$(HtmlEnc $b.Text)</div>" }
        }

        # Abschnitte
        $rows = ""
        foreach ($abschnitt in $Script:Report.Keys) {
            $rows += "<h2>$(HtmlEnc $abschnitt)</h2><table>"
            foreach ($e in $Script:Report[$abschnitt]) {
                $rows += "<tr><td class='label'>$(HtmlEnc $e.Label)</td><td class='$($e.Stufe)'>$(HtmlEnc $e.Wert)</td></tr>"
            }
            $rows += "</table>"
        }

        $html = @"
<!DOCTYPE html><html lang="de"><head><meta charset="utf-8">
<title>PC-Schnellcheck - $env:COMPUTERNAME</title>
<style>
  body{font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;color:#1a2433;margin:0;padding:32px;}
  .wrap{max-width:860px;margin:0 auto;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08);}
  header{background:#0d1f3c;color:#fff;padding:24px 32px;}
  header h1{margin:0;font-size:20px;}
  header .sub{color:#7fa8e6;font-size:13px;margin-top:4px;}
  .content{padding:24px 32px;}
  .ampel{padding:10px 14px;border-radius:6px;margin:6px 0;font-size:14px;}
  .ampel.ok{background:#e6f6ea;color:#1a7f37;border-left:4px solid #2da44e;}
  .ampel.warn{background:#fff8e1;color:#9a6700;border-left:4px solid #e0b000;}
  .ampel.crit{background:#fdecea;color:#b3261e;border-left:4px solid #d32f2f;}
  h2{font-size:15px;color:#0d1f3c;margin:22px 0 6px;border-bottom:2px solid #eef1f5;padding-bottom:4px;}
  table{width:100%;border-collapse:collapse;font-size:13.5px;}
  td{padding:6px 8px;border-bottom:1px solid #f0f2f5;}
  td.label{color:#5a6877;width:230px;}
  td.ok{color:#1a7f37;} td.warn{color:#9a6700;font-weight:600;} td.crit{color:#b3261e;font-weight:600;}
  footer{padding:16px 32px;color:#8a96a3;font-size:12px;border-top:1px solid #eef1f5;}
</style></head>
<body><div class="wrap">
<header><h1>PC-Schnellcheck</h1>
<div class="sub">$env:COMPUTERNAME &middot; erstellt am $(Get-Date -Format 'dd.MM.yyyy HH:mm') &middot; DariaTech IT-Systemhaus</div></header>
<div class="content">
<h2>Zusammenfassung</h2>
$befundHtml
$rows
</div>
<footer>Automatisch erstellt mit dem DariaTech PC-Schnellcheck. Werte ohne Gewaehr.</footer>
</div></body></html>
"@

        $html | Out-File -FilePath $datei -Encoding UTF8
        Write-Host "  Bericht gespeichert: $datei" -ForegroundColor Cyan
        Invoke-Item $datei
    } catch {
        Write-Host "  Bericht konnte nicht gespeichert werden: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ""
