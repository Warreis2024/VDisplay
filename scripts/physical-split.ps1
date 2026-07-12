# Geriye uyumluluk — overlay yok, vm-split kullanir
param(
    [ValidateSet("primary", "dual", "hybrid", "all")]
    [string]$Mode = "dual"
)

$mapped = switch ($Mode) {
    "hybrid" { "primary" }
    "all"    { "dual" }
    default  { $Mode }
}

& (Join-Path $PSScriptRoot "vm-split.ps1") -Mode $mapped
exit $LASTEXITCODE
