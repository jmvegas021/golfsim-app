#!/usr/bin/env bash
# Cross-machine helper. On Windows prefer:  .\scripts\publish-ally.ps1
# Or skip building: download GsproLighting-windows-x64.zip from GitHub Releases.
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

if [[ ! -f "${OUT}/GsproLighting.exe" ]]; then
  echo "Publish failed: ${OUT}/GsproLighting.exe missing" >&2
  exit 1
fi

dotnet publish "${ROOT}/src/GsproLighting.App/GsproLighting.App.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "${OUT}/console"

echo "Published Windows tray app: ${OUT}/GsproLighting.exe"
echo "Published Windows console:  ${OUT}/console/GsproLighting.App.exe"
echo "Or download the Release ZIP: https://github.com/jmvegas021/golfsim-app/releases"
