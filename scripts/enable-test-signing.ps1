# Test imzalama ac (yonetici). Cikis kodlari:
#   0 = cekirdekte zaten AKTIF (kuruluma devam)
#   2 = BCD yazildi veya henuz aktif degil — YENIDEN BASLAT gerekli
#   1 = hata

$ErrorActionPreference = "Stop"
$BcdEdit = Join-Path $env:SystemRoot "System32\bcdedit.exe"

function Test-IsAdmin {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-BcdTestSigning {
    # PowerShell'de {current} scriptblock olmasin diye tek tirnak
    $output = & $BcdEdit /enum '{current}' 2>&1 | Out-String
    return ($output -match "testsigning\s+Yes")
}

function Test-KernelTestSigning {
    try {
        if (-not ("VDisplayBoot.CiQuery" -as [type])) {
            Add-Type -Namespace VDisplayBoot -Name CiQuery -MemberDefinition @"
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
        }
        return [VDisplayBoot.CiQuery]::IsTestSignActive()
    } catch {
        return $false
    }
}

function Test-SecureBootOn {
    try {
        return [bool](Confirm-SecureBootUEFI)
    } catch {
        return $false
    }
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

if (-not (Test-IsAdmin)) {
    Write-Log "HATA: Script YONETICI degil. UAC onayina Evet deyin veya Yardimci'yi yonetici olarak acin."
    exit 1
}

if (-not (Test-Path $BcdEdit)) {
    Write-Log "HATA: bcdedit bulunamadi: $BcdEdit"
    exit 1
}

Write-Log ("Kullanici: {0}" -f ([Security.Principal.WindowsIdentity]::GetCurrent().Name))
Write-Log ("Yonetici: Evet")

$kernelOn = Test-KernelTestSigning
$bcdOn = Test-BcdTestSigning
Write-Log ("Cekirdek testsigning: {0}" -f $(if ($kernelOn) { "AKTIF" } else { "kapali" }))
Write-Log ("BCD testsigning: {0}" -f $(if ($bcdOn) { "Yes" } else { "No/bilinmiyor" }))

if ($kernelOn) {
    Write-Log "Test imzalama CEKIRDEKTE aktif. Kuruluma devam edilebilir."
    exit 0
}

if (-not $bcdOn) {
    Write-Log "Test imzalama BCD'ye yaziliyor..."
    $global:LASTEXITCODE = 0
    # '{current}' zorunlu — PowerShell {current}'i scriptblock sanmasin
    $bcdOut = & $BcdEdit /set '{current}' testsigning on 2>&1
    $bcdCode = $LASTEXITCODE
    if ($null -ne $bcdOut) {
        ($bcdOut | Out-String).Trim().Split("`n") | ForEach-Object {
            if ($_.Trim().Length -gt 0) { Write-Log ("  bcdedit: " + $_.Trim()) }
        }
    }

    if ($bcdCode -ne 0) {
        Write-Log ("HATA: bcdedit basarisiz (kod={0})." -f $bcdCode)
        Write-Log "Denenecekler:"
        Write-Log "  1) Baslat -> PowerShell -> Sag tik -> Yonetici olarak calistir"
        Write-Log "  2) Komut: bcdedit /set `{current`} testsigning on"
        Write-Log "  3) BIOS/UEFI: Secure Boot KAPAT (cok sik neden)"
        Write-Log "  4) Sonra bilgisayari yeniden baslat"
        if (Test-SecureBootOn) {
            Write-Log "NOT: Secure Boot ACIK gorunuyor — testsigning icin genelde kapatilmali."
        }
        exit 1
    }

    Write-Log "BCD guncellendi (testsigning on)."
}

Write-Log "Test imzalama henuz cekirdekte AKTIF DEGIL."
Write-Log "Bilgisayari SIMDI YENIDEN BASLATIN (masaüstünde Test Mode yazisi cikmali)."
Write-Log "Sonra Yardimci -> 0. Ilk kurulum tekrar."
if (Test-SecureBootOn) {
    Write-Log "UYARI: Secure Boot ACIK. Reboot sonrasi Test Mode gelmezse BIOS'ta Secure Boot'u kapat."
}
exit 2
