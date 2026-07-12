$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\VDisplay.Tray\VDisplay.Tray.csproj"
dotnet run --project $project
exit $LASTEXITCODE
