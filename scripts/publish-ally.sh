#!/usr/bin/env bash
# Cross-machine helper. On Windows prefer:  .\scripts\publish-ally.ps1
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}:/usr/local/share/dotnet"

OUT="${ROOT}/dist/ally"
mkdir -p "${OUT}"

dotnet publish "${ROOT}/src/GsproLighting.Ui/GsproLighting.Ui.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableWindowsTargeting=true \
  -o "${OUT}"

dotnet publish "${ROOT}/src/GsproLighting.App/GsproLighting.App.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "${OUT}/console"

echo "Published Windows tray app: ${OUT}/GsproLighting.exe"
echo "Published Windows console:  ${OUT}/console/GsproLighting.App.exe"
echo "Copy ${OUT}/ to your GSPro Windows PC / Ally X and run GsproLighting.exe"
