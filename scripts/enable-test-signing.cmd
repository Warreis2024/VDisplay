@echo off
setlocal EnableExtensions
REM Cikis: 0=cekirdek/BCD hazir say (Helper C# kontrol eder), 2=BCD yazildi reboot lazim, 1=hata
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

REM BCD'de zaten Yes mi?
"%BCDEDIT%" /enum {current} > "%TEMP%\vdisplay-bcd.txt" 2>&1
findstr /I /C:"testsigning" "%TEMP%\vdisplay-bcd.txt" | findstr /I "Yes" >nul
if not errorlevel 1 (
  echo [%TIME%] BCD testsigning zaten Yes. Reboot sonrasi cekirdek aktif olmali.>> "%LOG%"
  echo [%TIME%] Bilgisayari YENIDEN BASLAT, sonra 0. Ilk kurulum tekrar.>> "%LOG%"
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
  echo [%TIME%] Elle Yonetici CMD: bcdedit /set {current} testsigning on>> "%LOG%"
  echo [%TIME%] Secure Boot aciksa BIOS'ta kapat.>> "%LOG%"
  exit /b 1
)

echo [%TIME%] BCD guncellendi. SIMDI YENIDEN BASLAT ^(Test Mode^).>> "%LOG%"
echo [%TIME%] Sonra Yardimci -^> 0. Ilk kurulum tekrar.>> "%LOG%"
exit /b 2
