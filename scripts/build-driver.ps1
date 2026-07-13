param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "driver\VDisplayDriver\VDisplayDriver.vcxproj"
$expectedOutput = Join-Path $root "driver\VDisplayDriver\$Platform\$Configuration\VDisplayDriver"

function Find-MSBuild {
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )
    return $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Get-WdkBuildTargets {
    $buildRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\build"
    if (-not (Test-Path $buildRoot)) { return $null }

    return Get-ChildItem $buildRoot -Recurse -Filter "WindowsDriver.Common.targets" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}

function Test-WdkVsExtension {
    $vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath 2>$null
    if (-not $vsPath) { return $false }

    $toolset = Join-Path $vsPath "MSBuild\Microsoft\VC\v170\Platforms\x64\PlatformToolsets\WindowsUserModeDriver10.0"
    return Test-Path $toolset
}

function Test-WdkBuildForVs2022 {
    $wdk261 = "${env:ProgramFiles(x86)}\Windows Kits\10\build\10.0.26100.0\bin\Microsoft.DriverKit.Build.Tasks.17.0.dll"
    $wdk280 = "${env:ProgramFiles(x86)}\Windows Kits\10\build\10.0.28000.0\bin\Microsoft.DriverKit.Build.Tasks.18.0.dll"
    $sdk261 = "${env:ProgramFiles(x86)}\Windows Kits\10\DesignTime\CommonConfiguration\Neutral\UAP\10.0.26100.0\UAP.props"

    return @{
        HasWdk261 = Test-Path $wdk261
        HasWdk280 = Test-Path $wdk280
        HasSdk261 = Test-Path $sdk261
    }
}

function Ensure-AsciiInf {
    param([string]$InfPath)
    if (-not (Test-Path $InfPath)) { return }
    $bytes = [System.IO.File]::ReadAllBytes($InfPath)
    if ($bytes.Length -ge 2 -and (($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or $bytes[1] -eq 0x00)) {
        $text = [System.Text.Encoding]::Unicode.GetString($bytes)
        [System.IO.File]::WriteAllText($InfPath, $text, [System.Text.Encoding]::ASCII)
        Write-Host "INF ASCII'ye donusturuldu: $InfPath" -ForegroundColor DarkGray
    }
}

function Repair-WdkStampInf {
    $kitRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $brokenDir = Join-Path $kitRoot "10.0.26100.0"
    $sources = @(
        (Join-Path $kitRoot "x86\stampinf.exe"),
        (Join-Path $kitRoot "10.0.28000.0\x86\stampinf.exe")
    )
    $goodX86 = $sources | Where-Object { (Test-Path $_) -and ((Get-Item $_).Length -gt 10000) } | Select-Object -First 1
    if (-not $goodX86) {
        Write-Host "UYARI: Calisan stampinf.exe bulunamadi; StampInf adimi basarisiz olabilir." -ForegroundColor Yellow
        return
    }

    foreach ($arch in @("x86", "x64")) {
        $target = Join-Path $brokenDir "$arch\stampinf.exe"
        if (-not (Test-Path $target)) { continue }
        if ((Get-Item $target).Length -lt 10000) {
            $source = if ($arch -eq "x64") {
                Join-Path $kitRoot "10.0.28000.0\x64\stampinf.exe"
            } else {
                $goodX86
            }
            if ((Test-Path $source) -and ((Get-Item $source).Length -gt 10000)) {
                Copy-Item $source $target -Force
                Write-Host "stampinf onarildi: $target" -ForegroundColor DarkGray
            }
        }
    }
}

Write-Host "VDisplay surucu derlemesi..." -ForegroundColor Cyan

Repair-WdkStampInf
Ensure-AsciiInf -InfPath (Join-Path $root "driver\VDisplayDriver\VDisplayDriver.inf")

$wdkTargets = Get-WdkBuildTargets
$vsExtension = Test-WdkVsExtension
$wdkCompat = Test-WdkBuildForVs2022

if ($wdkCompat.HasWdk280 -and -not $wdkCompat.HasWdk261) {
    Write-Host ""
    Write-Host "UYARI: WDK 28000 kurulu ama VS 2022 ile uyumsuz." -ForegroundColor Yellow
    Write-Host "VS 2022 icin WDK 26100 kurmaniz gerekiyor:"
    Write-Host "  https://learn.microsoft.com/windows-hardware/drivers/other-wdk-downloads"
    Write-Host ""
}

if (-not $wdkTargets) {
    Write-Host ""
    Write-Host "HATA: WDK dosyalari bulunamadi." -ForegroundColor Red
    Write-Host "Once WDK MSI kurun: https://learn.microsoft.com/windows-hardware/drivers/download-the-wdk"
    exit 1
}

if (-not $vsExtension) {
    Write-Host ""
    Write-Host "HATA: Visual Studio WDK eklentisi eksik." -ForegroundColor Red
    Write-Host ""
    Write-Host "WDK dosyalari var ama VS entegrasyonu yok (yari kurulum)." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Cozum:" -ForegroundColor Yellow
    Write-Host "  Visual Studio Installer -> Degistir -> Bireysel bilesenler"
    Write-Host "  -> 'Windows Driver Kit' isaretle -> Degistir"
    Write-Host ""
    Write-Host "Detayli kontrol: .\check-wdk.ps1"
    exit 1
}

if (-not $wdkCompat.HasWdk261) {
    Write-Host "HATA: WDK 10.0.26100.0 build araclari bulunamadi." -ForegroundColor Red
    Write-Host "VS 2022 + bu proje icin WDK 26100 kurun (WDK 28000 yeterli degil)."
    exit 1
}

if (-not $wdkCompat.HasSdk261) {
    Write-Host "HATA: Windows SDK 10.0.26100.0 eksik." -ForegroundColor Red
    Write-Host "VS Installer -> Bagimsiz bilesenler -> Windows 11 SDK (10.0.26100)"
    exit 1
}

$msbuild = Find-MSBuild
if (-not $msbuild) {
    throw "MSBuild bulunamadi. Visual Studio 2022 kurulu mu?"
}

Write-Host "WDK: $wdkTargets" -ForegroundColor DarkGray
Write-Host "MSBuild: $msbuild"
# SignMode=Off: yerel WDKTestCert private-key sorunlarinda derleme silinmesin.
# Imza: .\scripts\publish-driver-package.ps1
& $msbuild $project /p:Configuration=$Configuration /p:Platform=$Platform /p:SignMode=Off /m

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "HATA: Derleme basarisiz." -ForegroundColor Red
    exit 1
}

$files = @("VDisplayDriver.inf", "VDisplayDriver.dll") | ForEach-Object {
    Join-Path $expectedOutput $_
}

$missing = $files | Where-Object { -not (Test-Path $_) }
$hasCat = Get-ChildItem $expectedOutput -Filter "*.cat" -ErrorAction SilentlyContinue
if ($missing -or -not $hasCat) {
    Write-Host ""
    Write-Host "UYARI: Derleme tamamlandi ama beklenen dosyalar eksik:" -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host ""
Write-Host "Derleme basarili." -ForegroundColor Green
Write-Host "Cikti: $expectedOutput"
Write-Host ""
Write-Host "Sonraki adim (yonetici): .\install-driver.ps1"
