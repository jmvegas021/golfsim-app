#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

function Invoke-DotNetPublish([string]$Project, [string]$OutputDir) {
    & dotnet publish $Project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $OutputDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $Project (exit $LASTEXITCODE). Install the .NET 8 SDK, or download GsproLighting-windows-x64.zip from GitHub Releases."
    }
}

$Root = Split-Path -Parent $PSScriptRoot
$Out = Join-Path $Root "dist\ally"
New-Item -ItemType Directory -Force -Path $Out | Out-Null

$sdks = & dotnet --list-sdks 2>$null
if (-not $sdks) {
    throw @"
No .NET SDK found.

You downloaded source code, which must be built — or skip building entirely:
  1. Open https://github.com/jmvegas021/golfsim-app/releases
  2. Download GsproLighting-windows-x64.zip
  3. Unzip and double-click GsproLighting.exe
"@
}

Write-Host "Publishing tray app (win-x64 self-contained)..."
Invoke-DotNetPublish (Join-Path $Root "src\GsproLighting.Ui\GsproLighting.Ui.csproj") $Out

$exe = Join-Path $Out "GsproLighting.exe"
if (-not (Test-Path $exe)) {
    throw "Publish finished but $exe was not created."
}

$ConsoleOut = Join-Path $Out "console"
New-Item -ItemType Directory -Force -Path $ConsoleOut | Out-Null

Write-Host "Publishing console tools..."
Invoke-DotNetPublish (Join-Path $Root "src\GsproLighting.App\GsproLighting.App.csproj") $ConsoleOut

Write-Host ""
Write-Host "Tray app:  $exe"
Write-Host "Console:   $(Join-Path $ConsoleOut 'GsproLighting.App.exe')"
Write-Host "Run the tray app, or use the Release ZIP on machines without the SDK."
