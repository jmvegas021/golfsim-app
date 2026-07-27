@echo off
setlocal EnableExtensions
cd /d "%~dp0"

REM ============================================================
REM  GSPro Lighting launcher
REM  For day-to-day use, download the Release ZIP (ready .exe):
REM    https://github.com/jmvegas021/golfsim-app/releases
REM  This .bat is only for building from SOURCE (needs .NET SDK).
REM ============================================================

set "EXE=%~dp0dist\ally\GsproLighting.exe"
set "RELEASE_URL=https://github.com/jmvegas021/golfsim-app/releases"

if exist "%EXE%" (
  start "" "%EXE%"
  exit /b 0
)

echo.
echo ============================================================
echo  GSPro Lighting — no built app found in this folder
echo ============================================================
echo.
echo  You likely downloaded the SOURCE code ^(Code -^> Download ZIP^).
echo  That ZIP cannot run by itself — it has to be built first.
echo.
echo  EASIEST FIX ^(recommended^):
echo    1. Open:  %RELEASE_URL%
echo    2. Download:  GsproLighting-windows-x64.zip
echo    3. Unzip that file
echo    4. Double-click:  GsproLighting.exe
echo.
echo  ^(Do NOT use "Code -^> Download ZIP" if you just want to run it.^)
echo.

where dotnet >nul 2>&1
if errorlevel 1 goto :NoSdk

REM Runtime-only installs have "dotnet" but no SDK — check for real SDKs.
dotnet --list-sdks 2>nul | findstr /R /C:"^[0-9]" >nul
if errorlevel 1 goto :NoSdk

echo  .NET SDK detected. Press Y to build from source, or any other key to open Releases.
choice /C YN /N /M "Build now [Y] or open Releases [N]? "
if errorlevel 2 goto :OpenRelease
if errorlevel 1 goto :Build
goto :OpenRelease

:NoSdk
echo  Press any key to open the Releases page in your browser...
pause >nul
goto :OpenRelease

:OpenRelease
start "" "%RELEASE_URL%"
echo.
echo  After downloading GsproLighting-windows-x64.zip, unzip it and
echo  double-click GsproLighting.exe inside.
echo.
pause
exit /b 1

:Build
echo.
echo  Building Windows .exe ^(one-time, needs network^)...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish-ally.ps1"
if errorlevel 1 goto :BuildFailed
if not exist "%EXE%" goto :BuildFailed

start "" "%EXE%"
exit /b 0

:BuildFailed
echo.
echo  Build failed. Use the Release ZIP instead — no build needed:
echo    %RELEASE_URL%
echo.
start "" "%RELEASE_URL%"
pause
exit /b 1
