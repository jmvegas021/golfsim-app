#!/usr/bin/env bash
# Create/publish GitHub Release with Velopack artifacts + portable zip.
# Usage: ./scripts/publish-github-release.sh [version]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-${HOME}/.dotnet}"
export PATH="${DOTNET_ROOT}:${DOTNET_ROOT}/tools:${HOME}/.dotnet/tools:${PATH}:/usr/local/share/dotnet"

VERSION="${1:-}"
if [[ -z "${VERSION}" ]]; then
  VERSION="$(grep -m1 '<Version>' "${ROOT}/Directory.Build.props" | sed -E 's/.*<Version>(.*)<\/Version>.*/\1/')"
fi
TAG="v${VERSION}"
REPO="jmvegas021/golfsim-app"
REPO_URL="https://github.com/${REPO}"
RELEASES="${ROOT}/dist/Releases"
ZIP="${ROOT}/dist/GsproLighting-windows-x64.zip"

bash "${ROOT}/scripts/package-release.sh" "${VERSION}"

NOTES="$(mktemp)"
cat > "${NOTES}" <<EOF
## GSPro Lighting ${TAG}

### Ally / end-user (recommended)
1. Download **\`GsproLighting-win-Setup.exe\`**
2. Run Setup once (installs under your user profile — Velopack-managed)
3. Open the tray app → **Settings → Updates**
4. Later: tray **Check for updates…** or the Updates section installs new releases

### Portable zip (still supported)
- Asset: \`GsproLighting-windows-x64.zip\`
- Settings → Updates uses the zip updater when not installed via Setup

### Developer
Packaged with Velopack from \`scripts/package-release.sh\`.
EOF

# Prefer vpk upload for Velopack assets (RELEASES / nupkg / Setup)
if [[ -d "${RELEASES}" ]]; then
  vpk '[win]' upload github \
    --repoUrl "${REPO_URL}" \
    --outputDir "${RELEASES}" \
    --publish true \
    --tag "${TAG}" \
    --releaseName "v${VERSION} — auto-update + lighting" \
    --merge true \
    -y || true
fi

# Ensure release exists and attach portable zip + any Setup if vpk merge left gaps
if ! gh release view "${TAG}" -R "${REPO}" >/dev/null 2>&1; then
  gh release create "${TAG}" \
    -R "${REPO}" \
    --title "v${VERSION} — auto-update + lighting" \
    --notes-file "${NOTES}"
else
  gh release edit "${TAG}" -R "${REPO}" --notes-file "${NOTES}" || true
fi

SETUP="$(ls "${RELEASES}"/*Setup.exe 2>/dev/null | head -1 || true)"
if [[ -n "${SETUP}" ]]; then
  gh release upload "${TAG}" -R "${REPO}" "${SETUP}" --clobber
fi

# Upload all Velopack channel files
shopt -s nullglob
for f in "${RELEASES}"/*; do
  [[ -f "${f}" ]] || continue
  gh release upload "${TAG}" -R "${REPO}" "${f}" --clobber || true
done

if [[ -f "${ZIP}" ]]; then
  gh release upload "${TAG}" -R "${REPO}" "${ZIP}" --clobber
fi

# Velopack GithubSource (no token) uses browser_download_url for these names.
# Fail the publish if the public feed is not reachable (private repo / bad upload).
FEED_URL="${REPO_URL}/releases/download/${TAG}/releases.win.json"
echo "Verifying Velopack feed: ${FEED_URL}"
for attempt in 1 2 3 4 5; do
  code="$(curl -sI -o /dev/null -w '%{http_code}' -L -A 'Velopack' "${FEED_URL}" || true)"
  if [[ "${code}" == "200" ]]; then
    break
  fi
  echo "  attempt ${attempt}: HTTP ${code} — retrying…"
  sleep 2
done
if [[ "${code}" != "200" ]]; then
  echo "ERROR: Velopack feed not publicly downloadable (HTTP ${code})." >&2
  echo "  Repo must be public (or pass a GithubSource accessToken)." >&2
  echo "  Expected asset: releases.win.json on ${TAG}" >&2
  exit 1
fi
curl -sL -A 'Velopack' "${FEED_URL}" | head -c 400
echo

rm -f "${NOTES}"
echo "Release: ${REPO_URL}/releases/tag/${TAG}"
