param(
    [string]$InfPath = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$defaultInf = Join-Path $root "driver\VDisplayDriver\x64\Release\VDisplayDriver\VDisplayDriver.inf"

if (-not $InfPath) { $InfPath = $defaultInf }
if (-not (Test-Path $InfPath)) { throw "INF bulunamadi: $InfPath" }

$instanceId = "SWD\VDISPLAYDRIVER\VDISPLAYDRIVER"

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NewDev {
    [DllImport("newdev.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool UpdateDriverForPlugAndPlayDevices(
        IntPtr hwndParent,
        string HardwareId,
        string FullInfPath,
        uint InstallFlags,
        out bool bRebootRequired);
}
"@

Write-Host "1) Surucu durduruluyor..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "ipc-client.ps1") -Command StopDriver | Out-Null

Write-Host "2) Eski cihaz kaldiriliyor..." -ForegroundColor Cyan
pnputil /remove-device $instanceId 2>&1 | Out-Null

Write-Host "3) Surucu paketi kuruluyor..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "install-driver.ps1")

Write-Host "4) Yazilim cihazi olusturuluyor..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "ipc-client.ps1") -Command StartDriver | Out-Host

Start-Sleep -Seconds 3

Write-Host "5) Surucu cihaza baglaniyor..." -ForegroundColor Cyan
$reboot = $false
$hwIds = @("VDisplayDriver", "Root\VDisplayDriver", "SWD\VDISPLAYDRIVER\VDISPLAYDRIVER")
$bound = $false
foreach ($hw in $hwIds) {
    if ([NewDev]::UpdateDriverForPlugAndPlayDevices([IntPtr]::Zero, $hw, $InfPath, 0, [ref]$reboot)) {
        Write-Host "   Basarili: $hw" -ForegroundColor Green
        $bound = $true
        break
    }
}

if (-not $bound) {
    Write-Host "   Otomatik baglama basarisiz. Aygit Yoneticisi -> VDisplay -> Surucu guncelle -> oem57.inf" -ForegroundColor Yellow
}

pnputil /restart-device $instanceId 2>&1 | Out-Null
Start-Sleep -Seconds 5

$dev = Get-PnpDevice -InstanceId $instanceId -ErrorAction SilentlyContinue
if ($dev) {
    Write-Host ""
    Write-Host "Cihaz: $($dev.FriendlyName)" -ForegroundColor Cyan
    Write-Host "Durum: $($dev.Status) / Problem: $($dev.Problem) / Sinif: $($dev.Class)"
}

$monitors = @(Get-PnpDevice -Class Monitor -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq "OK" })
Write-Host "Monitör sayisi (PnP): $($monitors.Count)"
