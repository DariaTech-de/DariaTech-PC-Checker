<#
.SYNOPSIS
  Erstellt die portable Single-File-.exe des DariaTech PC-Doktor.

.DESCRIPTION
  Veroeffentlicht das Projekt self-contained fuer win-x64 als eine einzelne
  .exe (keine .NET-Installation beim Kunden noetig). WPF unterstuetzt kein
  vollstaendiges Trimming -> daher self-contained ohne aggressives Trimming
  (~150 MB).

.PARAMETER Configuration
  Build-Konfiguration (Standard: Release).

.PARAMETER Output
  Zielordner fuer die .exe (Standard: artifacts\).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File build\publish.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\DariaTech.PcDoctor\DariaTech.PcDoctor.csproj"
$outDir = Join-Path $root $Output

Write-Host "Veroeffentliche DariaTech PC-Doktor ($Configuration, win-x64) ..." -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen (Code $LASTEXITCODE)." }

$exe = Join-Path $outDir "DariaTech.PcDoctor.exe"
Write-Host "Fertig: $exe" -ForegroundColor Green
Write-Host "Naechster Schritt (optional): build\sign.ps1 -Exe `"$exe`"" -ForegroundColor DarkGray
