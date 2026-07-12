param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\VDisplay.App\VDisplay.App.csproj"

dotnet build $project -c $Configuration -p:Platform=x64 --no-incremental
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$out = Join-Path $root "src\VDisplay.App\bin\x64\$Configuration\net8.0-windows10.0.19041.0"
$exe = Join-Path $out "VDisplay.App.exe"
if (-not (Test-Path $exe)) { throw "EXE bulunamadi: $exe" }

$bootstrap = Join-Path $out "runtimes\win-x64\native\Microsoft.WindowsAppRuntime.Bootstrap.dll"
if (Test-Path $bootstrap) {
    Copy-Item $bootstrap $out -Force
}

Write-Host "Baslatiliyor: $exe" -ForegroundColor Cyan
Write-Host "Not: WinApp SDK 1.5 + sistem runtime kullaniliyor." -ForegroundColor DarkGray
$p = Start-Process $exe -WorkingDirectory $out -PassThru
Start-Sleep -Seconds 2
if (Get-Process -Id $p.Id -ErrorAction SilentlyContinue) {
    Write-Host "VDisplay acildi (PID $($p.Id))" -ForegroundColor Green
} else {
    Write-Host "VDisplay hemen kapandi (exit $($p.ExitCode))" -ForegroundColor Red
    exit 1
}
