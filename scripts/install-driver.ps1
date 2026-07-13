param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$packageDir = Join-Path $root "driver\VDisplayDriver\$Platform\$Configuration\VDisplayDriver"
$distDir = Join-Path $root "dist\driver"
$logDir = Join-Path $env:ProgramData "VDisplay"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir "install-driver.log"
[System.IO.File]::WriteAllText($logFile, "", (New-Object System.Text.UTF8Encoding $false))

function Write-Log([string]$msg) {
    $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $msg
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::AppendAllText($logFile, $line + [Environment]::NewLine, $utf8)
    Write-Host $msg
}

function Test-Admin {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-TestSigningEnabled {
    # Running kernel only - BCD can show Yes before reboot
    if (-not ("VDisplayInstall.CiQuery" -as [type])) {
        try {
            Add-Type -Namespace VDisplayInstall -Name CiQuery -MemberDefinition @"
using System;
using System.Runtime.InteropServices;
public static class CiQuery {
  [StructLayout(LayoutKind.Sequential)]
  public struct SYSTEM_CODEINTEGRITY_INFORMATION {
    public uint Length;
    public uint CodeIntegrityOptions;
  }
  [DllImport("ntdll.dll")]
  public static extern int NtQuerySystemInformation(int SystemInformationClass, ref SYSTEM_CODEINTEGRITY_INFORMATION SystemInformation, int SystemInformationLength, out int ReturnLength);
  public static bool IsTestSignActive() {
    var info = new SYSTEM_CODEINTEGRITY_INFORMATION();
    info.Length = (uint)Marshal.SizeOf(info);
    int ret;
    int st = NtQuerySystemInformation(0x67, ref info, (int)info.Length, out ret);
    if (st != 0) return false;
    return (info.CodeIntegrityOptions & 0x2) != 0;
  }
}
"@ | Out-Null
        } catch {
            return $false
        }
    }

    try {
        return [VDisplayInstall.CiQuery]::IsTestSignActive()
    } catch {
        return $false
    }
}

function Install-PackageCertificate {
    param([string]$Dir)

    $cer = Join-Path $Dir "VDisplayTestCert.cer"
    if (-not (Test-Path $cer)) {
        Write-Log "UYARI: Pakette VDisplayTestCert.cer yok."
        return $false
    }

    Write-Log "Test sertifikasi kuruluyor: $cer"
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

Write-Log "VDisplay surucu kurulumu..."

if (-not (Test-Admin)) {
    Write-Log "HATA: Yonetici olarak calistirilmali."
    exit 1
}

if (-not (Test-TestSigningEnabled)) {
    Write-Log "HATA: Test imzalama henuz AKTIF DEGIL (reboot gerekli)."
    Write-Log "  1) .\scripts\enable-test-signing.cmd"
    Write-Log "  2) Bilgisayari YENIDEN BASLAT"
    Write-Log "  3) Masaustunde 'Test Mode' / 'Sinama Modu' yazisi gorunmeli"
    Write-Log "  4) Yardimci -> 0. Ilk kurulum tekrar"
    exit 1
}

$package = Find-DriverPackage -Dir $distDir
if (-not $package) {
    $package = Find-DriverPackage -Dir $packageDir
}

if (-not $package) {
    Write-Log "HATA: Surucu paketi bulunamadi."
    Write-Log "  Beklenen: $distDir"
    exit 1
}

Install-PackageCertificate -Dir $package.Dir | Out-Null

Write-Log "Paket: $($package.Dir)"
Write-Log "pnputil ile kuruluyor..."

$pnputilOutput = pnputil /add-driver $package.Inf /install 2>&1
$pnputilOutput | ForEach-Object { Write-Log "$_" }

$joined = ($pnputilOutput | Out-String)
if ($LASTEXITCODE -ne 0 -or ($joined -match "Failed|basarisiz|Error")) {
    Write-Log "HATA: Surucu depoya eklenemedi (pnputil kod=$LASTEXITCODE)."
    Write-Log "Log: $logFile"
    exit 1
}

Write-Log "Surucu depoya eklendi."
Write-Log "Sonraki adim: Yardimci -> 1. Baslat"
Write-Log "Sari unlem kalirsa: Aygit Yoneticisi -> Diskim var -> dist\driver\VDisplayDriver.inf"
exit 0
