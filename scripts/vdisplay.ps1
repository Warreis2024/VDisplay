param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\VDisplay.Cli\VDisplay.Cli.csproj"

dotnet run --project $project -- @Args

exit $LASTEXITCODE
