#!/usr/bin/env pwsh
# Build win-x64 tray app, Velopack release, and portable zip (Windows host).
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Version = if ($args.Count -ge 1 -and $args[0]) {
    $args[0]
} else {
    ([xml](Get-Content (Join-Path $Root "Directory.Build.props"))).Project.PropertyGroup.Version
}

Write-Host "Packaging GSPro Lighting v$Version"
& (Join-Path $PSScriptRoot "publish-ally.ps1")

$Ally = Join-Path $Root "dist\ally"
$PackDir = Join-Path $Root "dist\velopack-pack"
$Releases = Join-Path $Root "dist\Releases"
$Stage = Join-Path $Root "dist\release-stage"
$Zip = Join-Path $Root "dist\GsproLighting-windows-x64.zip"

foreach ($p in @($PackDir, $Releases, $Stage)) {
    if (Test-Path $p) { Remove-Item -Recurse -Force $p }
    New-Item -ItemType Directory -Force -Path $p | Out-Null
}

Copy-Item (Join-Path $Ally "GsproLighting.exe") $PackDir
Copy-Item (Join-Path $Ally "config") $PackDir -Recurse -ErrorAction SilentlyContinue

$vpk = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpk) {
    Write-Host "Installing vpk…"
    dotnet tool install -g vpk
}

# On Windows, default vpk target is win — no [win] directive required.
& vpk pack `
    -u GsproLighting `
    -v $Version `
    -p $PackDir `
    -e GsproLighting.exe `
    -o $Releases `
    --packTitle "GSPro Lighting" `
    --packAuthors "jmvegas021" `
    -y
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed ($LASTEXITCODE)" }

Copy-Item (Join-Path $Ally "GsproLighting.exe") $Stage
Copy-Item (Join-Path $Ally "config") $Stage -Recurse -ErrorAction SilentlyContinue
@"
GSPro Lighting — Windows (portable zip)

Preferred: install via GsproLighting-win-Setup.exe from GitHub Releases.
Settings → Updates for check / install. Tray: Check for updates…
"@ | Set-Content -Path (Join-Path $Stage "README.txt") -Encoding UTF8

if (Test-Path $Zip) { Remove-Item -Force $Zip }
Compress-Archive -Path (Join-Path $Stage "*") -DestinationPath $Zip -Force

Write-Host ""
Write-Host "Velopack Releases: $Releases"
Write-Host "Portable zip:      $Zip"
Write-Host "Upload: vpk upload github --repoUrl https://github.com/jmvegas021/golfsim-app --outputDir `"$Releases`" --publish --tag v$Version --merge"
Write-Host "         gh release upload v$Version `"$Zip`" --clobber"
