@echo off
setlocal EnableExtensions
REM Exit: 0=ok, 2=BCD written reboot needed, 1=error / Secure Boot
set "LOG=%ProgramData%\VDisplay\enable-test-signing.log"
set "BCDEDIT=%SystemRoot%\System32\bcdedit.exe"
if exist "%SystemRoot%\Sysnative\bcdedit.exe" set "BCDEDIT=%SystemRoot%\Sysnative\bcdedit.exe"

mkdir "%ProgramData%\VDisplay" 2>nul
echo [%TIME%] enable-test-signing.cmd basladi> "%LOG%"
echo [%TIME%] bcdedit=%BCDEDIT%>> "%LOG%"
echo [%TIME%] user=%USERNAME%>> "%LOG%"

net session >nul 2>&1
if errorlevel 1 (
  echo [%TIME%] HATA: Yonetici degil. UAC'de Evet deyin.>> "%LOG%"
  exit /b 1
)
echo [%TIME%] Yonetici: Evet>> "%LOG%"

if not exist "%BCDEDIT%" (
  echo [%TIME%] HATA: bcdedit bulunamadi>> "%LOG%"
  exit /b 1
)

REM Secure Boot check
powershell -NoProfile -Command "try { if (Confirm-SecureBootUEFI) { exit 11 } else { exit 0 } } catch { exit 0 }" >nul 2>&1
if errorlevel 11 (
  echo [%TIME%] HATA: Secure Boot / Guvenli Onyukleme ACIK.>> "%LOG%"
  echo [%TIME%] Test imzali surucu icin BIOS/UEFI'de Secure Boot KAPATILMALI.>> "%LOG%"
  echo [%TIME%] Adimlar:>> "%LOG%"
  echo [%TIME%]   1^) PC yeniden baslat -^> BIOS/UEFI ^(Del/F2/F10^)>> "%LOG%"
  echo [%TIME%]   2^) Secure Boot = Disabled>> "%LOG%"
  echo [%TIME%]   3^) Kaydet, Windows'a gir>> "%LOG%"
  echo [%TIME%]   4^) Yardimci -^> 0. Ilk kurulum>> "%LOG%"
  exit /b 1
)

"%BCDEDIT%" /enum {current} > "%TEMP%\vdisplay-bcd.txt" 2>&1
findstr /I /C:"testsigning" "%TEMP%\vdisplay-bcd.txt" | findstr /I "Yes" >nul
if not errorlevel 1 (
  echo [%TIME%] BCD testsigning zaten Yes. Yeniden baslat, sonra 0. Ilk kurulum.>> "%LOG%"
  exit /b 2
)

echo [%TIME%] BCD'ye yaziliyor: testsigning on>> "%LOG%"
"%BCDEDIT%" /set {current} testsigning on >> "%LOG%" 2>&1
if errorlevel 1 (
  echo [%TIME%] Yedek: /set testsigning on>> "%LOG%"
  "%BCDEDIT%" /set testsigning on >> "%LOG%" 2>&1
)
if errorlevel 1 (
  echo [%TIME%] HATA: bcdedit basarisiz.>> "%LOG%"
  echo [%TIME%] Bu hata genelde Secure Boot yuzunden. BIOS'ta Secure Boot KAPAT.>> "%LOG%"
  exit /b 1
)

echo [%TIME%] BCD guncellendi. SIMDI YENIDEN BASLAT ^(Test Mode^).>> "%LOG%"
exit /b 2
