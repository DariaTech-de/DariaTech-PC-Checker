<#
.SYNOPSIS
  Erstellt die portable Single-File-.exe des DariaTech PC-Doktor.

.DESCRIPTION
  Veroeffentlicht das Projekt self-contained fuer win-x64 als eine einzelne
  .exe (keine .NET-Installation beim Kunden noetig). WPF unterstuetzt kein
  vollstaendiges Trimming -> daher self-contained ohne aggressives Trimming
  (~150 MB).

  Mit -Pin wird der Zugangsschutz aktiviert: Aus dem PIN werden ein
  Zufallssalz und ein PBKDF2-Hash erzeugt und in die .exe eingebettet. Der
  PIN selbst landet NIE im Quellcode und ist aus dem Hash nicht
  rueckrechenbar. Ohne -Pin entsteht ein ungeschuetzter Entwickler-Build.

.PARAMETER Configuration
  Build-Konfiguration (Standard: Release).

.PARAMETER Output
  Zielordner fuer die .exe (Standard: artifacts\).

.PARAMETER Pin
  Zugangs-PIN (mindestens 8 Zeichen). Wird nur zum Erzeugen des Hashes
  verwendet und nicht gespeichert. Am besten als SecureString uebergeben,
  damit der PIN nicht in der PowerShell-Historie landet.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File build\publish.ps1

.EXAMPLE
  # Mit Zugangsschutz - PIN wird verdeckt abgefragt:
  powershell -ExecutionPolicy Bypass -File build\publish.ps1 -Pin (Read-Host -AsSecureString "PIN")
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts",
    [object]$Pin
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\DariaTech.PcDoctor\DariaTech.PcDoctor.csproj"
$outDir = Join-Path $root $Output

# --- Zugangs-PIN vorbereiten (optional) ---
$pinArgs = @()
if ($Pin) {
    # SecureString ist zu bevorzugen; Klartext wird aus Bequemlichkeit akzeptiert.
    if ($Pin -is [System.Security.SecureString]) {
        $plain = [System.Net.NetworkCredential]::new("", $Pin).Password
    } else {
        $plain = [string]$Pin
    }

    if ($plain.Length -lt 8) {
        throw "Der PIN muss mindestens 8 Zeichen lang sein (angegeben: $($plain.Length))."
    }
    if (($plain.ToCharArray() | Select-Object -Unique).Count -eq 1) {
        throw "Der PIN besteht nur aus einem sich wiederholenden Zeichen - bitte einen anderen waehlen."
    }

    $iterations = 600000
    $saltBytes = [byte[]]::new(16)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($saltBytes)

    # Muss zu Core\Security\PinHasher.cs passen: PBKDF2 / SHA-256 / 32 Byte.
    $kdf = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
        $plain, $saltBytes, $iterations, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        $hashBytes = $kdf.GetBytes(32)
    } finally {
        $kdf.Dispose()
    }

    $pinArgs = @(
        "-p:PinSalt=$([Convert]::ToBase64String($saltBytes))",
        "-p:PinHash=$([Convert]::ToBase64String($hashBytes))",
        "-p:PinIterations=$iterations"
    )

    $plain = $null   # Klartext nicht laenger vorhalten
    Write-Host "Zugangsschutz aktiv: PIN-Hash wird in die .exe eingebettet." -ForegroundColor Yellow
} else {
    Write-Host "HINWEIS: Ohne -Pin entsteht ein UNGESCHUETZTER Build (jeder kann die App bedienen)." -ForegroundColor DarkYellow
}

Write-Host "Veroeffentliche DariaTech PC-Doktor ($Configuration, win-x64) ..." -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    @pinArgs `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen (Code $LASTEXITCODE)." }

$exe = Join-Path $outDir "DariaTech.PcDoctor.exe"
Write-Host "Fertig: $exe" -ForegroundColor Green
if ($pinArgs.Count -gt 0) {
    Write-Host "Beim Start wird nun der PIN verlangt; nach 30 Minuten ohne Bedienung sperrt die App erneut." -ForegroundColor Green
}
Write-Host "Naechster Schritt (optional): build\sign.ps1 -Exe `"$exe`"" -ForegroundColor DarkGray
