param(
    [ValidateSet("Debug", "Release", "All")]
    [string]$Configuration = "All"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $root "native\VDisplayNative"
$vcvars = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"

if (-not (Test-Path $vcvars)) {
    throw "Visual Studio 2022 vcvars64 bulunamadi."
}

$configs = if ($Configuration -eq "All") { @("Debug", "Release") } else { @($Configuration) }

$buildCmd = @"
call "$vcvars" >nul
cd /d "$sourceDir"
cl /nologo /LD VDisplayNative.cpp /Fe:VDisplayNative.dll swdevice.lib cfgmgr32.lib
"@

cmd /c $buildCmd
if ($LASTEXITCODE -ne 0) {
    throw "VDisplayNative.dll derlenemedi."
}

$dll = Join-Path $sourceDir "VDisplayNative.dll"
if (-not (Test-Path $dll)) {
    throw "VDisplayNative.dll olusturulamadi."
}

foreach ($cfg in $configs) {
    $outDir = Join-Path $root "src\VDisplay.Service\bin\$cfg\net8.0-windows"
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    Copy-Item $dll $outDir -Force
    Write-Host "VDisplayNative.dll -> $outDir" -ForegroundColor Green
}

$distNative = Join-Path $root "dist\native"
New-Item -ItemType Directory -Force -Path $distNative | Out-Null
Copy-Item $dll $distNative -Force
Write-Host "VDisplayNative.dll -> $distNative" -ForegroundColor Green
