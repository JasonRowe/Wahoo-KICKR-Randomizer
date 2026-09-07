#!/usr/bin/env bash
# Fetch the latest nightly build of BikeFitnessApp (Linux/Avalonia), install, and launch.
set -euo pipefail

REPO="JasonRowe/Wahoo-KICKR-Randomizer"
ASSET="bikefitness-linux-x64.zip"
INSTALL_DIR="${BIKEFITNESS_INSTALL_DIR:-$HOME/.local/share/BikeFitnessApp}"
EXE="BikeFitness.Avalonia"
URL="https://github.com/${REPO}/releases/download/nightly/${ASSET}"

mkdir -p "${INSTALL_DIR}"
tmp="$(mktemp --suffix=.zip)"
trap 'rm -f "${tmp}"' EXIT

echo "Downloading ${ASSET} ..."
curl -fL --retry 3 -o "${tmp}" "${URL}"

echo "Installing to ${INSTALL_DIR} ..."
unzip -oq "${tmp}" -d "${INSTALL_DIR}"

chmod +x "${INSTALL_DIR}/${EXE}" 2>/dev/null || true
echo "Launching ${EXE} ..."
exec "${INSTALL_DIR}/${EXE}"
