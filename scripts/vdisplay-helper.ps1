$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $root "src\VDisplay.Helper\VDisplay.Helper.csproj") -c Release
exit $LASTEXITCODE
