# Test imzalama ac (yonetici, yeniden baslatma gerekir)
$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "Bu script yonetici olarak calistirilmali."
}

Write-Host "Test imzalama aciliyor..." -ForegroundColor Cyan
bcdedit /set testsigning on

Write-Host ""
Write-Host "Test imzalama acildi. Degisiklik icin bilgisayari YENIDEN BASLATIN." -ForegroundColor Green
Write-Host "Yeniden baslattiktan sonra:"
Write-Host "  1. driver\VDisplayDriver.sln -> Release|x64 derle"
Write-Host "  2. .\scripts\install-driver.ps1"
