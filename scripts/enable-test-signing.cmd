@echo off
setlocal EnableExtensions
REM Exit: 0=ok, 2=BCD written reboot needed, 1=error / Secure Boot
REM Log messages are ASCII-only to avoid mojibake in Helper (UTF-8 vs OEM).
set "LOG=%ProgramData%\VDisplay\enable-test-signing.log"
set "BCDEDIT=%SystemRoot%\System32\bcdedit.exe"
if exist "%SystemRoot%\Sysnative\bcdedit.exe" set "BCDEDIT=%SystemRoot%\Sysnative\bcdedit.exe"

mkdir "%ProgramData%\VDisplay" 2>nul
> "%LOG%" echo [%TIME%] enable-test-signing.cmd started
>> "%LOG%" echo [%TIME%] bcdedit=%BCDEDIT%
>> "%LOG%" echo [%TIME%] user=%USERNAME%

net session >nul 2>&1
if errorlevel 1 (
  >> "%LOG%" echo [%TIME%] ERROR: not elevated. Click Yes on UAC.
  exit /b 1
)
>> "%LOG%" echo [%TIME%] Admin: Yes

if not exist "%BCDEDIT%" (
  >> "%LOG%" echo [%TIME%] ERROR: bcdedit not found
  exit /b 1
)

powershell -NoProfile -Command "try { if (Confirm-SecureBootUEFI) { exit 11 } else { exit 0 } } catch { exit 0 }" >nul 2>&1
if errorlevel 11 (
  >> "%LOG%" echo [%TIME%] ERROR: Secure Boot is ON.
  >> "%LOG%" echo [%TIME%] Disable Secure Boot in BIOS/UEFI, save, boot Windows, then run 0. First setup again.
  exit /b 1
)

"%BCDEDIT%" /enum {current} > "%TEMP%\vdisplay-bcd.txt" 2>&1
findstr /I /C:"testsigning" "%TEMP%\vdisplay-bcd.txt" | findstr /I "Yes" >nul
if not errorlevel 1 (
  >> "%LOG%" echo [%TIME%] BCD testsigning already Yes. REBOOT now, then run 0. First setup again.
  exit /b 2
)

>> "%LOG%" echo [%TIME%] Writing BCD: testsigning on
"%BCDEDIT%" /set {current} testsigning on > "%TEMP%\vdisplay-bcd-set.txt" 2>&1
set "BCDERR=%ERRORLEVEL%"
REM Do not append raw bcdedit locale text (mojibake). Log success/fail only.
if not "%BCDERR%"=="0" (
  >> "%LOG%" echo [%TIME%] Retry: bcdedit /set testsigning on
  "%BCDEDIT%" /set testsigning on > "%TEMP%\vdisplay-bcd-set.txt" 2>&1
  set "BCDERR=%ERRORLEVEL%"
)
if not "%BCDERR%"=="0" (
  >> "%LOG%" echo [%TIME%] ERROR: bcdedit failed (code=%BCDERR%).
  >> "%LOG%" echo [%TIME%] If Secure Boot is ON, disable it in BIOS. Then retry.
  exit /b 1
)

>> "%LOG%" echo [%TIME%] BCD updated OK. REBOOT now (desktop should show Test Mode).
>> "%LOG%" echo [%TIME%] After reboot: Helper -^> 0. First setup again.
exit /b 2
