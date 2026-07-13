@echo off
setlocal EnableExtensions
REM Fix missing function driver: remove SW device, reinstall package, create, bind INF.
set "LOG=%ProgramData%\VDisplay\repair-device.log"
set "ROOT=%~dp0.."
set "INF=%ROOT%\dist\driver\VDisplayDriver.inf"
mkdir "%ProgramData%\VDisplay" 2>nul
> "%LOG%" echo [%TIME%] repair-vdisplay-device.cmd started

net session >nul 2>&1
if errorlevel 1 (
  >> "%LOG%" echo [%TIME%] ERROR: not elevated
  exit /b 1
)

cd /d "%ROOT%"

>> "%LOG%" echo [%TIME%] Stopping processes
taskkill /F /IM VDisplay.Service.exe >nul 2>&1
taskkill /F /IM VDisplay.Tray.exe >nul 2>&1
timeout /t 2 /nobreak >nul

>> "%LOG%" echo [%TIME%] Removing SWD device
pnputil /remove-device "SWD\VDisplayDriver\VDisplayDriver" >> "%LOG%" 2>&1

>> "%LOG%" echo [%TIME%] Reinstalling driver package
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-driver.ps1"
if errorlevel 1 (
  >> "%LOG%" echo [%TIME%] install-driver failed
  exit /b 1
)

>> "%LOG%" echo [%TIME%] Starting service
start "" "%ROOT%\src\VDisplay.Service\bin\Release\net8.0-windows\VDisplay.Service.exe"
timeout /t 4 /nobreak >nul

>> "%LOG%" echo [%TIME%] driver start
dotnet run --project "%ROOT%\src\VDisplay.Cli\VDisplay.Cli.csproj" -c Release --no-build -- driver start >> "%LOG%" 2>&1
timeout /t 2 /nobreak >nul

>> "%LOG%" echo [%TIME%] Binding INF
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0bind-driver.ps1" >> "%LOG%" 2>&1

timeout /t 3 /nobreak >nul
pnputil /add-driver "%INF%" /install >> "%LOG%" 2>&1
pnputil /enum-devices /instanceid "SWD\VDisplayDriver\VDisplayDriver" >> "%LOG%" 2>&1
>> "%LOG%" echo [%TIME%] done
exit /b 0
