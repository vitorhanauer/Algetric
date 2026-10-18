#!/usr/bin/env bash
# Shared locations keep the installed application separate from projects and tile data.
set -euo pipefail
algetric_data_root="${XDG_DATA_HOME:-$HOME/.local/share}"
algetric_bin_root="${ALGETRIC_BIN_DIR:-$HOME/.local/bin}"
for algetric_path in "$algetric_data_root" "$algetric_bin_root"; do
  if [[ "$algetric_path" != /* || "$algetric_path" == *$'\n'* || "$algetric_path" == *$'\r'* ]]; then
    printf 'Installation paths must be absolute and must not contain newlines.\n' >&2
    exit 1
  fi
done
algetric_app="$algetric_data_root/algetric/application"
algetric_launcher="$algetric_bin_root/algetric"
algetric_desktop="$algetric_data_root/applications/algetric.desktop"
algetric_icon="$algetric_data_root/icons/hicolor/scalable/apps/algetric.svg"

algetric_is_managed() {
  [[ ! -L "$algetric_app" && -f "$algetric_app/.algetric-install" ]] &&
    [[ "$(cat "$algetric_app/.algetric-install")" == 'algetric-user-install-v1' ]]
}

algetric_refresh_menu() {
  if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$algetric_data_root/applications" || true
  fi
  if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "$algetric_data_root/icons/hicolor" >/dev/null 2>&1 || true
  fi
}

algetric_exec_quote() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//\$/\\\$}"
  value="${value//\`/\\\`}"
  value="${value//%/%%}"
  # Desktop string escaping is decoded before Exec argument quoting.
  value="${value//\\/\\\\}"
  printf '"%s"' "$value"
}
