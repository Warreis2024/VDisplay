param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$srcInf = Join-Path $root "driver\VDisplayDriver\VDisplayDriver.inf"
$pkg = Join-Path $root "driver\VDisplayDriver\$Platform\$Configuration\VDisplayDriver"
$stampInf = Join-Path $root "driver\VDisplayDriver\$Platform\$Configuration\VDisplayDriver.inf"
$inf2cat = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.26100.0\x86\inf2cat.exe"
$signtool = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"

if (-not (Test-Path $inf2cat)) { throw "inf2cat bulunamadi: $inf2cat" }

# Kaynak INF ASCII olmali (inf2cat UTF-16 ile bozuluyor). DriverVer ve UmdfLibraryVersion kaynakta hazir.
$bytes = [System.IO.File]::ReadAllBytes($srcInf)
if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
    throw "VDisplayDriver.inf UTF-16; ASCII olarak kaydedin."
}
if ($bytes.Length -ge 2 -and $bytes[1] -eq 0x00) {
    throw "VDisplayDriver.inf UTF-16 LE; ASCII olarak kaydedin."
}

$ascii = [System.Text.Encoding]::ASCII
$infText = $ascii.GetString($bytes)
[System.IO.File]::WriteAllText($stampInf, $infText, $ascii)

New-Item -ItemType Directory -Force -Path $pkg | Out-Null
Copy-Item (Join-Path $root "driver\VDisplayDriver\$Platform\$Configuration\VDisplayDriver.dll") $pkg -Force
Copy-Item $stampInf (Join-Path $pkg "VDisplayDriver.inf") -Force

Remove-Item (Join-Path $pkg "*.cat") -Force -ErrorAction SilentlyContinue
& $inf2cat /os:10_x64 /driver:$pkg
if ($LASTEXITCODE -ne 0) { throw "inf2cat basarisiz" }

$cat = Get-ChildItem $pkg -Filter "*.cat" | Select-Object -First 1
$thumb = (Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*WDKTestCert*" } | Select-Object -First 1).Thumbprint
& $signtool sign /ph /fd sha256 /sha1 $thumb $cat.FullName

Write-Host "Paket hazir: $pkg" -ForegroundColor Green
