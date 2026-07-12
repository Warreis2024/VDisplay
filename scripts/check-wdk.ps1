param()

function Get-WdkBuildTargets {
    $buildRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\build"
    if (-not (Test-Path $buildRoot)) { return $null }

    return Get-ChildItem $buildRoot -Recurse -Filter "WindowsDriver.Common.targets" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}

function Test-WdkVsExtension {
    $vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath 2>$null
    if (-not $vsPath) { return $false }

    $toolsetDirs = @(
        (Join-Path $vsPath "MSBuild\Microsoft\VC\v170\Platforms\x64\PlatformToolsets\WindowsUserModeDriver10.0"),
        (Join-Path $vsPath "MSBuild\Microsoft\VC\v170\Platforms\Win32\PlatformToolsets\WindowsUserModeDriver10.0")
    )

    return ($toolsetDirs | Where-Object { Test-Path $_ }).Count -gt 0
}

function Get-VsVersion {
    $version = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property catalog_productDisplayVersion 2>$null
    return $version
}

function Test-VsVersionSupportsWdkComponent {
    param([string]$Version)

    if ([string]::IsNullOrWhiteSpace($Version)) { return $false }

    $parts = $Version.Split('.')
    if ($parts.Count -lt 2) { return $false }

    $minor = [int]$parts[1]
    return $minor -ge 11
}

$wdkTargets = Get-WdkBuildTargets
$vsExtension = Test-WdkVsExtension
$vsVersion = Get-VsVersion
$vsSupportsWdk = Test-VsVersionSupportsWdkComponent $vsVersion

Write-Host "=== VDisplay WDK Durum Kontrolu ===" -ForegroundColor Cyan
Write-Host ""

if ($vsVersion) {
    Write-Host "Visual Studio surumu: $vsVersion"
    if ($vsSupportsWdk) {
        Write-Host "[OK] VS surumu WDK bilesenini destekliyor (17.11+)" -ForegroundColor Green
    } else {
        Write-Host "[!]  VS surumu eski (17.10). WDK bileseni icin 17.11+ gerekli." -ForegroundColor Yellow
    }
    Write-Host ""
}

if ($wdkTargets) {
    Write-Host "[OK] WDK dosyalari kurulu" -ForegroundColor Green
    Write-Host "     $wdkTargets"
} else {
    Write-Host "[X]  WDK dosyalari bulunamadi" -ForegroundColor Red
}

Write-Host ""

if ($vsExtension) {
    Write-Host "[OK] Visual Studio WDK eklentisi kurulu" -ForegroundColor Green
} else {
    Write-Host "[X]  Visual Studio WDK eklentisi EKSIK" -ForegroundColor Red
    Write-Host "     Bu yuzden 'WindowsUserModeDriver10.0' hatasi aliyorsunuz."
}

Write-Host ""

if ($wdkTargets -and -not $vsExtension) {
    Write-Host "DURUM: WDK yari kurulu." -ForegroundColor Yellow
    Write-Host "wdksetup.exe = WDK dosyalari (tamam)"
    Write-Host "Eksik = Visual Studio WDK eklentisi (ayri adim)"
    Write-Host ""
    Write-Host "Cozum (sirayla):" -ForegroundColor Yellow
    Write-Host "  1. VS Installer penceresini KAPAT, tekrar ac"
    Write-Host "  2. Is yukleri -> 'C++ ile masaustu gelistirme' ISARETLE (zorunlu)"
    Write-Host "  3. Bagimsiz bilesenler -> arama kutusunu SIL, asagi kaydir"
    Write-Host "     'SDK''lar, kitapliklar ve cerceveler' altinda 'Windows Driver Kit' ara"
    Write-Host "  4. Bulamazsan: WDK 28000 VS 2026 icin. VS 2022 icin WDK 26100 kur:"
    Write-Host "     https://learn.microsoft.com/windows-hardware/drivers/other-wdk-downloads"
    Write-Host "  5. Kurulum bitince VS'yi yeniden baslat -> .\build-driver.ps1"
    exit 2
}

if (-not $wdkTargets) {
    Write-Host "Cozum: https://learn.microsoft.com/windows-hardware/drivers/download-the-wdk" -ForegroundColor Yellow
    exit 1
}

Write-Host "Her sey hazir. Derlemek icin: .\build-driver.ps1" -ForegroundColor Green
exit 0
