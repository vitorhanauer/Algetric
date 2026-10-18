#!/usr/bin/env bash
set -euo pipefail
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
source "$script_directory/linux-common.sh"
if [[ ! -e "$algetric_app" && ! -L "$algetric_app" ]]; then
  printf 'Algetric is not installed.\n'
  exit 0
fi
if ! algetric_is_managed; then
  printf 'Refusing to remove an unmanaged application directory: %s\n' "$algetric_app" >&2
  exit 1
fi
if [[ -L "$algetric_launcher" && "$(readlink "$algetric_launcher")" == "$algetric_app/Algetric.Desktop" ]]; then
  rm -- "$algetric_launcher"
fi
if [[ -f "$algetric_desktop" ]] && grep -qx 'X-Algetric-Managed=true' "$algetric_desktop"; then
  rm -- "$algetric_desktop"
fi
rm -f -- "$algetric_icon"
rm -rf -- "$algetric_app"
algetric_refresh_menu
printf 'Algetric uninstalled. Projects, settings, credentials, and downloaded maps were preserved.\n'
