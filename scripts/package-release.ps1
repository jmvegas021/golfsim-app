#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot "publish-ally.ps1")

$Ally = Join-Path $Root "dist\ally"
$Stage = Join-Path $Root "dist\release-stage"
$Zip = Join-Path $Root "dist\GsproLighting-windows-x64.zip"

if (Test-Path $Stage) { Remove-Item -Recurse -Force $Stage }
New-Item -ItemType Directory -Force -Path $Stage | Out-Null

Copy-Item (Join-Path $Ally "GsproLighting.exe") $Stage
Copy-Item (Join-Path $Ally "config") $Stage -Recurse -ErrorAction SilentlyContinue

# Include any native/deps next to the single-file exe if present
Get-ChildItem $Ally -File | Where-Object {
    $_.Name -ne "GsproLighting.exe" -and $_.Extension -in ".dll", ".json", ".pdb"
} | ForEach-Object { Copy-Item $_.FullName $Stage -ErrorAction SilentlyContinue }

@"
GSPro Reactive Lighting — Windows

1. Double-click GsproLighting.exe
2. Set your WLED IP in Settings → Test lights
3. Start GSPro + GSPro Connect (Garmin R50) as usual
4. Leave R50 auto-watch on — Connect log ball metrics drive the live feed + WLED
5. Hit balls — expect [Ready] / [Shot] / [Putt] lines and light flashes

Optional Open Connect proxy: point an LM/bridge at 127.0.0.1:1921

Tray icon: right-click for settings / test lights / exit.

Repo: https://github.com/jmvegas021/golfsim-app
"@ | Set-Content -Path (Join-Path $Stage "README.txt") -Encoding UTF8

if (Test-Path $Zip) { Remove-Item -Force $Zip }
Compress-Archive -Path (Join-Path $Stage "*") -DestinationPath $Zip -Force

Write-Host ""
Write-Host "Release zip: $Zip"
Write-Host "Unzip on any Windows GSPro PC and double-click GsproLighting.exe"
