#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
package_directory=artifacts/linux-x64
if [[ ! -x "$package_directory/Algetric.Desktop" || ! -f "$package_directory/Web/vendor/Cesium/Cesium.js" ]]; then
  printf 'Publish the application first with scripts/publish-linux.sh.\n' >&2
  exit 1
fi
install -m 755 scripts/install-linux.sh "$package_directory/install.sh"
install -m 755 scripts/uninstall-linux.sh "$package_directory/uninstall.sh"
install -m 644 scripts/linux-common.sh assets/algetric.svg "$package_directory/"
cp README.md THIRD_PARTY_NOTICES.md "$package_directory/"
cp -r docs "$package_directory/"
tar -czf artifacts/algetric-linux-x64.tar.gz -C "$package_directory" .
sha256sum artifacts/algetric-linux-x64.tar.gz > artifacts/algetric-linux-x64.tar.gz.sha256
