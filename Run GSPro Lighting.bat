@echo off
setlocal
cd /d "%~dp0"

REM Double-click this file on Windows to start GSPro Lighting.
REM Prefer downloading a Release ZIP if you don't want to build from source:
REM   https://github.com/jmvegas021/golfsim-app/releases

set "EXE=%~dp0dist\ally\GsproLighting.exe"

if exist "%EXE%" (
  start "" "%EXE%"
  exit /b 0
)

where dotnet >nul 2>&1
if errorlevel 1 (
  echo.
  echo GSPro Lighting is not built yet, and the .NET 8 SDK was not found.
  echo.
  echo Easiest fix — no build required:
  echo   1. Open https://github.com/jmvegas021/golfsim-app/releases
  echo   2. Download GsproLighting-windows-x64.zip
  echo   3. Unzip, then double-click GsproLighting.exe
  echo.
  pause
  exit /b 1
)

echo First launch: building a Windows .exe ^(one-time^)...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish-ally.ps1"
if errorlevel 1 (
  echo.
  echo Build failed. See messages above.
  pause
  exit /b 1
)

if not exist "%EXE%" (
  echo Expected file missing: %EXE%
  pause
  exit /b 1
)

start "" "%EXE%"
exit /b 0
