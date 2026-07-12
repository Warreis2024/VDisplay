param(
    [ValidateSet("hybrid", "all")]
    [string]$Mode = "all"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$overlay = Join-Path $root "src\VDisplay.Overlay\VDisplay.Overlay.csproj"
Write-Host "Overlay (dogrudan): $Mode" -ForegroundColor Cyan
dotnet run --project $overlay -- $Mode
exit $LASTEXITCODE
