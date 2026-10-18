#!/usr/bin/env bash
set -euo pipefail
repository="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository"
if [[ ! -f src/Algetric.Desktop/Web/vendor/Cesium/Cesium.js ]]; then
  npm ci --ignore-scripts --prefix src/Algetric.Desktop/Web
  npm run build --prefix src/Algetric.Desktop/Web
fi
exec dotnet run --project src/Algetric.Desktop -- "$@"
