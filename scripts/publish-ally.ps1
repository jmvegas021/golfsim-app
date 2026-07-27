#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Out = Join-Path $Root "dist\ally"
New-Item -ItemType Directory -Force -Path $Out | Out-Null

Write-Host "Publishing tray app (win-x64 self-contained)..."
dotnet publish (Join-Path $Root "src\GsproLighting.Ui\GsproLighting.Ui.csproj") `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $Out

$ConsoleOut = Join-Path $Out "console"
New-Item -ItemType Directory -Force -Path $ConsoleOut | Out-Null

Write-Host "Publishing console tools..."
dotnet publish (Join-Path $Root "src\GsproLighting.App\GsproLighting.App.csproj") `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $ConsoleOut

Write-Host ""
Write-Host "Tray app:  $Out\GsproLighting.exe"
Write-Host "Console:   $ConsoleOut\GsproLighting.App.exe"
Write-Host "Copy the dist\ally folder to your GSPro Windows PC / Ally X and run GsproLighting.exe"
