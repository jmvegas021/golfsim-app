#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"

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

# Also publish console spike tools for Ally debugging
dotnet publish "${ROOT}/src/GsproLighting.App/GsproLighting.App.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "${OUT}/console"

echo "Published tray app: ${OUT}/GsproLighting.exe"
echo "Published console:  ${OUT}/console/GsproLighting.App.exe"
echo "Copy ${OUT}/ to the Ally X and run GsproLighting.exe"
