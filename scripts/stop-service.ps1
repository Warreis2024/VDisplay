Get-Process VDisplay.Service -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host "VDisplay.Service durduruldu." -ForegroundColor Green
