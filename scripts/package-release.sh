#!/usr/bin/env bash
# Build win-x64 tray app, Velopack release (Setup.exe + nupkg), and portable zip.
# Run from macOS or Linux with: EnableWindowsTargeting + vpk tool.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${HOME}/.dotnet/tools:${PATH}:/usr/local/share/dotnet"
export DOTNET_ROOT="${HOME}/.dotnet"

VERSION="${1:-}"
if [[ -z "${VERSION}" ]]; then
  VERSION="$(grep -m1 '<Version>' "${ROOT}/Directory.Build.props" | sed -E 's/.*<Version>(.*)<\/Version>.*/\1/')"
fi

echo "Packaging GSPro Lighting v${VERSION}"

bash "${ROOT}/scripts/publish-ally.sh"

ALLY="${ROOT}/dist/ally"
PACK_DIR="${ROOT}/dist/velopack-pack"
RELEASES="${ROOT}/dist/Releases"
STAGE="${ROOT}/dist/release-stage"
ZIP="${ROOT}/dist/GsproLighting-windows-x64.zip"

rm -rf "${PACK_DIR}" "${RELEASES}" "${STAGE}"
mkdir -p "${PACK_DIR}" "${STAGE}"

cp "${ALLY}/GsproLighting.exe" "${PACK_DIR}/"
cp -R "${ALLY}/config" "${PACK_DIR}/" 2>/dev/null || true

# Velopack Windows packages from non-Windows hosts:
if ! command -v vpk >/dev/null 2>&1; then
  echo "Installing vpk global tool…"
  dotnet tool install -g vpk || dotnet tool update -g vpk
fi

vpk '[win]' pack \
  -u GsproLighting \
  -v "${VERSION}" \
  -p "${PACK_DIR}" \
  -e GsproLighting.exe \
  -o "${RELEASES}" \
  --packTitle "GSPro Lighting" \
  --packAuthors "jmvegas021" \
  -y

# Portable zip (same asset name as prior releases; used by zip-install updater)
cp "${ALLY}/GsproLighting.exe" "${STAGE}/"
cp -R "${ALLY}/config" "${STAGE}/" 2>/dev/null || true
cat > "${STAGE}/README.txt" <<EOF
GSPro Lighting — Windows (portable zip)

Preferred: install via GsproLighting-win-Setup.exe from GitHub Releases
so Velopack can auto-update under your user profile.

Portable zip:
1. Unzip anywhere
2. Double-click GsproLighting.exe
3. Settings → Updates → Check for updates (zip-based updater)

Tray: right-click → Check for updates…
Repo: https://github.com/jmvegas021/golfsim-app
EOF

rm -f "${ZIP}"
# Use ditto/zip available on macOS
(
  cd "${STAGE}"
  zip -qr "${ZIP}" .
)

echo ""
echo "Velopack Releases: ${RELEASES}"
ls -lah "${RELEASES}" || true
echo "Portable zip:      ${ZIP}"
echo ""
echo "Upload with (or use scripts/publish-github-release.sh):"
echo "  vpk '[win]' upload github --repoUrl https://github.com/jmvegas021/golfsim-app --outputDir ${RELEASES} --publish --tag v${VERSION} --merge"
echo "  gh release upload v${VERSION} ${ZIP} --clobber"
