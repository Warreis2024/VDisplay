# Test imzalama ac (yonetici). Cikis kodlari:
#   0 = cekirdekte zaten AKTIF (kuruluma devam)
#   2 = BCD yazildi veya henuz aktif degil - YENIDEN BASLAT gerekli
#   1 = hata

$ErrorActionPreference = "Continue"

function Get-BcdEditPath {
    $windir = $env:SystemRoot
    $sysnative = Join-Path $windir "Sysnative\bcdedit.exe"
    $system32 = Join-Path $windir "System32\bcdedit.exe"
    # 32-bit PowerShell'te System32 -> SysWOW64 yonlenir; Sysnative gercek 64-bit
    if (Test-Path $sysnative) { return $sysnative }
    return $system32
}

function Test-IsAdmin {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Invoke-BcdEdit {
    param([string[]]$Args)
    $exe = Get-BcdEditPath
    $out = & $exe @Args 2>&1
    return @{
        Exe = $exe
        Code = [int]$LASTEXITCODE
        Output = (($out | ForEach-Object { "$_" }) -join "`n").Trim()
    }
}

function Test-BcdTestSigning {
    $r = Invoke-BcdEdit -Args @('/enum')
    return ($r.Output -match "testsigning\s+Yes")
}

function Test-KernelTestSigning {
    try {
        if (-not ("VDisplayBoot.CiQuery" -as [type])) {
            Add-Type -Namespace VDisplayBoot -Name CiQuery -MemberDefinition @"
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
"@
        }
        return [VDisplayBoot.CiQuery]::IsTestSignActive()
    } catch {
        return $false
    }
}

function Test-SecureBootOn {
    try { return [bool](Confirm-SecureBootUEFI) } catch { return $false }
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

Write-Log ("Islem: {0}-bit | OS: {1}-bit" -f $(if ([Environment]::Is64BitProcess) { "64" } else { "32" }), $(if ([Environment]::Is64BitOperatingSystem) { "64" } else { "32" }))
Write-Log ("bcdedit: {0}" -f (Get-BcdEditPath))
Write-Log ("Kullanici: {0}" -f [Security.Principal.WindowsIdentity]::GetCurrent().Name)

if (-not (Test-IsAdmin)) {
    Write-Log "HATA: Script YONETICI degil. UAC de Evet e basin."
    exit 1
}
Write-Log "Yonetici: Evet"

$kernelOn = Test-KernelTestSigning
$bcdOn = Test-BcdTestSigning
Write-Log ("Cekirdek testsigning: {0}" -f $(if ($kernelOn) { "AKTIF" } else { "kapali" }))
Write-Log ("BCD testsigning: {0}" -f $(if ($bcdOn) { "Yes" } else { "No/bilinmiyor" }))

if ($kernelOn) {
    Write-Log "Test imzalama CEKIRDEKTE aktif. Kuruluma devam edilebilir."
    exit 0
}

if (-not $bcdOn) {
    Write-Log "Test imzalama BCD ye yaziliyor..."
    # Argument dizisi kullan - PowerShell {current} scriptblock sorununu onler
    $r = Invoke-BcdEdit -Args @('/set', '{current}', 'testsigning', 'on')
    if ($r.Output) {
        $r.Output.Split("`n") | ForEach-Object { if ($_.Trim()) { Write-Log ("  " + $_.Trim()) } }
    }

    if ($r.Code -ne 0) {
        # yedek: identifier olmadan
        Write-Log "Yedek deneme: bcdedit /set testsigning on"
        $r2 = Invoke-BcdEdit -Args @('/set', 'testsigning', 'on')
        if ($r2.Output) {
            $r2.Output.Split("`n") | ForEach-Object { if ($_.Trim()) { Write-Log ("  " + $_.Trim()) } }
        }
        $r = $r2
    }

    if ($r.Code -ne 0) {
        Write-Log ("HATA: bcdedit basarisiz (kod={0})." -f $r.Code)
        Write-Log "Elle dene (Yonetici PowerShell):"
        Write-Log "  bcdedit /set {current} testsigning on"
        Write-Log "Sonra bilgisayari yeniden baslat."
        if (Test-SecureBootOn) {
            Write-Log "NOT: Secure Boot ACIK - BIOS ta Secure Bootu kapatman gerekebilir."
        }
        Write-Log "Erisim engellendi ise: Yardimciyi kapat, Start-VDisplay.cmd sag tik -> Yonetici olarak calistir."
        exit 1
    }

    Write-Log "BCD guncellendi (testsigning on)."
}

Write-Log "Test imzalama henuz cekirdekte AKTIF DEGIL."
Write-Log "Bilgisayari SIMDI YENIDEN BASLATIN (masaustunde Test Mode yazisi cikmali)."
Write-Log "Sonra Yardimci -> 0. Ilk kurulum tekrar."
if (Test-SecureBootOn) {
    Write-Log "UYARI: Secure Boot ACIK. Test Mode gelmezse BIOS ta Secure Boot kapat."
}
exit 2