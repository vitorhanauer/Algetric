#!/usr/bin/env bash
set -euo pipefail
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
source "$script_directory/linux-common.sh"

package_directory="$script_directory"
if [[ ! -f "$package_directory/Algetric.Desktop" ]]; then
  package_directory="$script_directory/../artifacts/linux-x64"
  if [[ ! -x "$package_directory/Algetric.Desktop" ]]; then
    bash "$script_directory/publish-linux.sh"
  else
    bash "$script_directory/package-linux.sh"
  fi
fi
package_directory="$(cd -- "$package_directory" && pwd)"
for required in Algetric.Desktop Web/vendor/Cesium/Cesium.js algetric.svg uninstall.sh linux-common.sh; do
  if [[ ! -f "$package_directory/$required" ]]; then
    printf 'Incomplete application package: missing %s. Rebuild with scripts/publish-linux.sh.\n' "$required" >&2
    exit 1
  fi
done
if [[ ! -x "$package_directory/Algetric.Desktop" ]]; then
  printf 'The packaged application is not executable.\n' >&2
  exit 1
fi
if [[ -e "$algetric_app" || -L "$algetric_app" ]] && ! algetric_is_managed; then
  printf 'Refusing to replace an unmanaged application directory: %s\n' "$algetric_app" >&2
  exit 1
fi
if [[ -e "$algetric_launcher" || -L "$algetric_launcher" ]]; then
  if [[ ! -L "$algetric_launcher" || "$(readlink "$algetric_launcher")" != "$algetric_app/Algetric.Desktop" ]]; then
    printf 'Refusing to replace an unrelated launcher: %s\n' "$algetric_launcher" >&2
    exit 1
  fi
fi
if [[ -e "$algetric_desktop" ]] && ! grep -qx 'X-Algetric-Managed=true' "$algetric_desktop"; then
  printf 'Refusing to replace an unmanaged desktop entry.\n' >&2
  exit 1
fi
mkdir -p -- "$(dirname "$algetric_app")" "$algetric_bin_root" "$(dirname "$algetric_desktop")" "$(dirname "$algetric_icon")"
stage="$(mktemp -d "$(dirname "$algetric_app")/.application-stage.XXXXXX")"
backup=''
cleanup() {
  [[ ! -d "$stage" ]] || rm -rf -- "$stage"
  if [[ -n "$backup" && -d "$backup" && ! -e "$algetric_app" ]]; then
    mv -- "$backup" "$algetric_app"
  fi
}
trap cleanup EXIT
cp -a -- "$package_directory/." "$stage/"
printf 'algetric-user-install-v1\n' > "$stage/.algetric-install"
if [[ -d "$algetric_app" ]]; then
  backup="${stage}.previous"
  mv -- "$algetric_app" "$backup"
fi
mv -- "$stage" "$algetric_app"
cat > "$algetric_app/launch.sh" <<'LAUNCHER'
#!/usr/bin/env bash
set -euo pipefail
application_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec "$application_directory/Algetric.Desktop" "$@"
LAUNCHER
ln -sfn -- "$algetric_app/Algetric.Desktop" "$algetric_launcher"
install -m 644 -- "$algetric_app/algetric.svg" "$algetric_icon"
{
  printf '[Desktop Entry]\nType=Application\nName=Algetric\n'
  printf 'Comment=Edit geometries on a Cartesian plane and Earth globe\n'
  printf 'Exec=/bin/bash %s\n' "$(algetric_exec_quote "$algetric_app/launch.sh")"
  printf 'Icon=algetric\nTerminal=false\nCategories=Science;Geography;\n'
  printf 'Keywords=geometry;polygon;map;globe;GIS;\nStartupWMClass=Algetric.Desktop\nX-Algetric-Managed=true\n'
} > "$algetric_desktop"
chmod 644 "$algetric_desktop"
if [[ -n "$backup" ]]; then rm -rf -- "$backup"; fi
algetric_refresh_menu
printf 'Installed Algetric. Open it from your applications menu or run:\n  %s\n' "$algetric_launcher"
