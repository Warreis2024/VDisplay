param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$packageDir = Join-Path $root "driver\VDisplayDriver\$Platform\$Configuration\VDisplayDriver"
$distDir = Join-Path $root "dist\driver"

function Test-Admin {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-TestSigningEnabled {
    $output = bcdedit /enum "{current}" 2>$null
    return ($output -match "testsigning\s+Yes")
}

function Install-PackageCertificate {
    param([string]$Dir)

    $cer = Join-Path $Dir "VDisplayTestCert.cer"
    if (-not (Test-Path $cer)) {
        Write-Host "UYARI: Pakette VDisplayTestCert.cer yok." -ForegroundColor Yellow
        return $false
    }

    Write-Host "Test sertifikasi kuruluyor: $cer" -ForegroundColor Cyan
    certutil -addstore Root $cer | Out-Null
    certutil -addstore TrustedPublisher $cer | Out-Null
    return $true
}

function Find-DriverPackage {
    param([string]$Dir)

    if (-not (Test-Path $Dir)) { return $null }

    $inf = Join-Path $Dir "VDisplayDriver.inf"
    $dll = Join-Path $Dir "VDisplayDriver.dll"
    $cat = Get-ChildItem $Dir -Filter "*.cat" -ErrorAction SilentlyContinue | Select-Object -First 1

    if ((Test-Path $inf) -and (Test-Path $dll) -and $cat) {
        return @{ Inf = $inf; Dir = $Dir }
    }

    return $null
}

Write-Host "VDisplay surucu kurulumu..." -ForegroundColor Cyan

if (-not (Test-Admin)) {
    throw "Bu script yonetici olarak calistirilmali."
}

if (-not (Test-TestSigningEnabled)) {
    Write-Host "HATA: Test imzalama kapali. Once:" -ForegroundColor Red
    Write-Host "  .\scripts\enable-test-signing.ps1"
    Write-Host "  Yeniden baslat"
    exit 1
}

# Son kullanici: dist\driver (hazir paket). Gelistirici: build cikti klasoru.
$package = Find-DriverPackage -Dir $distDir
if (-not $package) {
    $package = Find-DriverPackage -Dir $packageDir
}

if (-not $package) {
    Write-Host "HATA: Surucu paketi bulunamadi." -ForegroundColor Red
    Write-Host "  Beklenen: $distDir"
    Write-Host "  (veya gelistirici build): $packageDir"
    Write-Host "Gelistirici: .\scripts\publish-driver-package.ps1"
    exit 1
}

Install-PackageCertificate -Dir $package.Dir | Out-Null

Write-Host "Paket: $($package.Dir)" -ForegroundColor Green
Write-Host "pnputil ile kuruluyor..."

$pnputilOutput = pnputil /add-driver $package.Inf /install 2>&1
$pnputilOutput | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -ne 0 -or ($pnputilOutput -match "Failed|basarisiz")) {
    Write-Host ""
    Write-Host "HATA: Surucu depoya eklenemedi." -ForegroundColor Red
    Write-Host "Test sertifikasi ve testsigning durumunu kontrol edin."
    exit 1
}

Write-Host ""
Write-Host "Surucu depoya eklendi." -ForegroundColor Green
Write-Host "Sonraki adim: Yardimci -> 1. Baslat"
