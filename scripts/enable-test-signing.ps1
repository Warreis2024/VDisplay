# Test imzalama ac (yonetici). Cikis kodlari:
#   0 = cekirdekte zaten AKTIF (kuruluma devam)
#   2 = BCD yazildi veya henuz aktif degil — YENIDEN BASLAT gerekli
#   1 = hata

$ErrorActionPreference = "Stop"

function Test-BcdTestSigning {
    $output = bcdedit /enum "{current}" 2>$null
    return ($output -match "testsigning\s+Yes")
}

# CODEINTEGRITY_OPTION_TESTSIGN = 0x2 — calisan cekirdek
function Test-KernelTestSigning {
    try {
        Add-Type -Namespace VDisplayBoot -Name CiQuery -ErrorAction Stop -MemberDefinition @"
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
        # tip zaten yuklu olabilir
    }

    try {
        return [VDisplayBoot.CiQuery]::IsTestSignActive()
    } catch {
        return $false
    }
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "Bu script yonetici olarak calistirilmali."
}

$logDir = Join-Path $env:ProgramData "VDisplay"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir "enable-test-signing.log"

function Write-Log([string]$msg) {
    $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $msg
    Add-Content -Path $logFile -Value $line -Encoding UTF8
    Write-Host $msg
}

"" | Set-Content -Path $logFile -Encoding UTF8

$kernelOn = Test-KernelTestSigning
$bcdOn = Test-BcdTestSigning

if ($kernelOn) {
    Write-Log "Test imzalama CEKIRDEKTE aktif. Kuruluma devam edilebilir."
    exit 0
}

if (-not $bcdOn) {
    Write-Log "Test imzalama BCD'ye yaziliyor..."
    bcdedit /set testsigning on
    if ($LASTEXITCODE -ne 0) {
        Write-Log "HATA: bcdedit basarisiz (kod=$LASTEXITCODE)."
        exit 1
    }
}

Write-Log "Test imzalama henuz cekirdekte AKTIF DEGIL."
Write-Log "Bilgisayari SIMDI YENIDEN BASLATIN (masaüstünde Test Mode yazisi cikmali)."
Write-Log "Sonra Yardimci -> 0. Ilk kurulum tekrar."
exit 2
