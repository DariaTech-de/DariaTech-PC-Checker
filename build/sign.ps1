<#
.SYNOPSIS
  Signiert die DariaTech-PC-Doktor-.exe mit dem DariaTech-Code-Signing-Zertifikat
  (gegen die SmartScreen-Warnung beim Kunden).

.DESCRIPTION
  Verwendet signtool.exe (Windows SDK). Zertifikat (.pfx) und Passwort NIEMALS
  ins Repo committen (siehe .gitignore). Passwort am besten ueber die
  Umgebungsvariable DARIATECH_PFX_PASSWORD bereitstellen.

.PARAMETER Exe
  Pfad zur zu signierenden .exe (Standard: artifacts\DariaTech.PcDoctor.exe).

.PARAMETER Pfx
  Pfad zum Code-Signing-Zertifikat (.pfx).

.PARAMETER Password
  Zertifikatspasswort. Fallback: Umgebungsvariable DARIATECH_PFX_PASSWORD.

.EXAMPLE
  $env:DARIATECH_PFX_PASSWORD = "geheim"
  powershell -ExecutionPolicy Bypass -File build\sign.ps1 -Pfx C:\zert\DariaTech-CodeSigning.pfx
#>
[CmdletBinding()]
param(
    [string]$Exe = "artifacts\DariaTech.PcDoctor.exe",
    [Parameter(Mandatory = $true)][string]$Pfx,
    [string]$Password = $env:DARIATECH_PFX_PASSWORD,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Exe)) { throw "Datei nicht gefunden: $Exe (zuerst build\publish.ps1 ausfuehren)." }
if (-not (Test-Path $Pfx)) { throw "Zertifikat nicht gefunden: $Pfx" }
if ([string]::IsNullOrWhiteSpace($Password)) {
    throw "Kein Passwort. -Password angeben oder Umgebungsvariable DARIATECH_PFX_PASSWORD setzen."
}

$signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if (-not $signtool) {
    throw "signtool.exe nicht gefunden. Bitte Windows SDK installieren bzw. die Developer-Eingabeaufforderung verwenden."
}

Write-Host "Signiere $Exe ..." -ForegroundColor Cyan
& signtool.exe sign /tr $TimestampUrl /td sha256 /fd sha256 /f $Pfx /p $Password $Exe
if ($LASTEXITCODE -ne 0) { throw "Signierung fehlgeschlagen (Code $LASTEXITCODE)." }

Write-Host "Pruefe Signatur ..." -ForegroundColor Cyan
& signtool.exe verify /pa /v $Exe
if ($LASTEXITCODE -ne 0) { throw "Signaturpruefung fehlgeschlagen (Code $LASTEXITCODE)." }

Write-Host "Signatur erfolgreich." -ForegroundColor Green
