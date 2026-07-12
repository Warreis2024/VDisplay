param(
    [ValidateSet("primary", "dual")]
    [string]$Mode = "dual"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$cli = Join-Path $root "src\VDisplay.Cli\VDisplay.Cli.csproj"

Write-Host "VM split kurulumu ($Mode)..."
dotnet run --project $cli -- driver start
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $cli -- vm-split setup $Mode
exit $LASTEXITCODE
