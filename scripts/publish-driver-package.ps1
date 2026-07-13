param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

# Gelistirici: build ciktilarini dist\driver'a kopyalar / imzalar.
# Son kullanici bu scripti calistirmaz.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$buildPkg = Join-Path $root "driver\VDisplayDriver\$Platform\$Configuration\VDisplayDriver"
$dist = Join-Path $root "dist\driver"
$signtool = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$inf2cat = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.26100.0\x86\inf2cat.exe"

if (-not (Test-Path (Join-Path $buildPkg "VDisplayDriver.dll"))) {
    Write-Host "Once surucuyu derleyin: .\scripts\build-driver.ps1 (SignMode=Off olabilir)" -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "build-driver.ps1") -Configuration $Configuration -Platform $Platform
}

if (-not (Test-Path $signtool)) { throw "signtool bulunamadi" }
if (-not (Test-Path $inf2cat)) { throw "inf2cat bulunamadi" }

New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item (Join-Path $buildPkg "VDisplayDriver.dll") $dist -Force
Copy-Item (Join-Path $buildPkg "VDisplayDriver.inf") $dist -Force
Remove-Item (Join-Path $dist "*.cat") -Force -ErrorAction SilentlyContinue

& $inf2cat /os:10_x64 /driver:$dist
if ($LASTEXITCODE -ne 0) { throw "inf2cat basarisiz" }

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq "CN=VDisplay Test Driver" -and $_.HasPrivateKey } |
    Select-Object -First 1

if (-not $cert) {
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=VDisplay Test Driver" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyExportPolicy Exportable `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(10)
}

& $signtool sign /ph /fd sha256 /sha1 $cert.Thumbprint (Join-Path $dist "VDisplayDriver.dll")
if ($LASTEXITCODE -ne 0) { throw "dll imza basarisiz" }

$cat = Get-ChildItem $dist -Filter "*.cat" | Select-Object -First 1
& $signtool sign /ph /fd sha256 /sha1 $cert.Thumbprint $cat.FullName
if ($LASTEXITCODE -ne 0) { throw "cat imza basarisiz" }

Export-Certificate -Cert $cert -FilePath (Join-Path $dist "VDisplayTestCert.cer") -Force | Out-Null

Write-Host ""
Write-Host "Hazir paket: $dist" -ForegroundColor Green
Get-ChildItem $dist | ForEach-Object { Write-Host ("  {0} ({1} byte)" -f $_.Name, $_.Length) }
Write-Host "Bu klasoru git'e commit edin; son kullanici yalnizca install-driver kullanir."
