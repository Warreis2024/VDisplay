# Bind published INF to the VDisplay software device (requires admin).
$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
$inf = Join-Path $root "dist\driver\VDisplayDriver.inf"
if (-not (Test-Path $inf)) {
    Write-Host "HATA: INF yok: $inf"
    exit 1
}

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class VDisplayNewDev {
    [DllImport("newdev.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool UpdateDriverForPlugAndPlayDevices(
        IntPtr hwndParent,
        string HardwareId,
        string FullInfPath,
        uint InstallFlags,
        out bool bRebootRequired);
}
"@

$flags = 0x1 -bor 0x4 # FORCE | NONINTERACTIVE
$bound = $false
foreach ($hw in @("VDisplayDriver", "Root\VDisplayDriver", "SWD\VDisplayDriver\VDisplayDriver")) {
    $reboot = $false
    $ok = [VDisplayNewDev]::UpdateDriverForPlugAndPlayDevices([IntPtr]::Zero, $hw, $inf, $flags, [ref]$reboot)
    $err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    Write-Host ("Bind {0}: ok={1} win32={2} reboot={3}" -f $hw, $ok, $err, $reboot)
    if ($ok) {
        $bound = $true
        break
    }
}

Start-Sleep -Seconds 2
$dev = Get-PnpDevice -InstanceId "SWD\VDISPLAYDRIVER\VDISPLAYDRIVER" -ErrorAction SilentlyContinue
if ($dev) {
    Write-Host ("Device Status={0} Problem={1} Class={2} Service={3}" -f $dev.Status, $dev.Problem, $dev.Class, $dev.Service)
    if ([string]::IsNullOrWhiteSpace([string]$dev.Service)) {
        Write-Host "HATA: Function driver baglanmadi (Service bos). Aygit Yoneticisi > Goruntu adaptörleri > VDisplay > Surucuyu Guncelle."
        exit 2
    }
}

if (-not $bound) {
    exit 1
}
exit 0
