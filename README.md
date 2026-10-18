# Algetric

Desktop editor for local geometries with a Cartesian plane and an Earth globe, built with .NET 10 and Avalonia for Linux x64. The interface follows `mockups/example.png`.

## Install as a desktop application

Install the native dependencies first. A published package includes the .NET runtime and does not require the .NET SDK or Node.js.

Arch Linux:

```sh
sudo pacman -S --needed proj gtk3 webkit2gtk-4.1 libsecret
```

Ubuntu 24.04:

```sh
sudo apt-get install libproj25 proj-data libgtk-3-0t64 libwebkit2gtk-4.1-0 libsecret-tools libgl1-mesa-dri
```

From this repository, install for your current user with:

```sh
bash scripts/install-linux.sh
```

This uses the published application in `artifacts/linux-x64`, building it first if it is missing. Building requires the source prerequisites below. To install an extracted Linux package instead, run this inside its directory:

```sh
bash install.sh
```

Run the installer without `sudo`. It copies the application into your user data directory, adds an icon and an **Algetric** entry to your applications menu, and creates `~/.local/bin/algetric`. You can open Algetric from the menu, pin it to your desktop environment's favorites, or run:

```sh
~/.local/bin/algetric
```

If `~/.local/bin` is already on your `PATH`, `algetric` works directly. The menu shortcut works independently of `PATH`. Installation copies all runtime and web assets, so the repository or extracted package can subsequently be moved or removed.

Close Algetric before updating. From the repository, build the latest code and reinstall:

```sh
bash scripts/publish-linux.sh
bash scripts/install-linux.sh
```

From an updated extracted package, rerun `bash install.sh`. To uninstall from the repository:

```sh
bash scripts/uninstall-linux.sh
```

Without the repository, use the installed uninstaller:

```sh
bash "${XDG_DATA_HOME:-$HOME/.local/share}/algetric/application/uninstall.sh"
```

Uninstalling removes the application, menu entry, icon, and command. Projects, settings, credentials, and downloaded maps are preserved. Installation honors `XDG_DATA_HOME`; `ALGETRIC_BIN_DIR` can override the command directory, which defaults to `~/.local/bin`.

## Run from source

Requirements: the SDK specified in `global.json`, Node.js 24, PROJ and its database, WebKitGTK 4.1, GTK3, and WebGL acceleration. An X11 session or a Wayland session with XWayland must be available. Bearer credentials require `secret-tool` and an unlocked Secret Service.

Arch Linux:

```sh
sudo pacman -S --needed dotnet-sdk nodejs npm proj gtk3 webkit2gtk-4.1 libsecret
```

Ubuntu 24.04 (also install the .NET SDK and Node.js):

```sh
sudo apt-get install libproj25 proj-data libgtk-3-0t64 libwebkit2gtk-4.1-0 libsecret-tools libgl1-mesa-dri
```

Run from the repository with one command:

```sh
bash scripts/run.sh
```

This prepares Cesium assets on the first run and starts the application through `dotnet run`. Arguments are forwarded, for example `bash scripts/run.sh --diagnostics`. You can still run the individual setup commands:

```sh
npm ci --ignore-scripts --prefix src/Algetric.Desktop/Web
npm run build --prefix src/Algetric.Desktop/Web
dotnet restore Algetric.sln --locked-mode
dotnet run --project src/Algetric.Desktop
```

Build the web assets before building the .NET application. The `Web/vendor` directory is generated from the Cesium package pinned in the npm lockfile. Its 22 MB of assets, including workers, fonts, and supporting files, are stored locally. No Cesium ion token is required.

## Usage

The application interface is currently in Portuguese. The instructions below use English translations of its labels.

1. Set the origin through **Map → Geographic origin**, or choose **Set origin on map** and click the Earth. The default is São Paulo (−23.55°, −46.63°).
2. Choose the layer for new objects. Create points, segments, polylines, or polygons by clicking the plane or map. Finish polylines and polygons with a double-click, Enter, or **Finish**. Esc cancels the drawing.
3. With **Select**, drag a vertex or the entire object. The sidebar lets you edit the name and coordinates of a vertex; **Apply** confirms a single history operation.
4. Use the mouse wheel to zoom. On the plane, drag with the right or middle mouse button to pan. The map uses Cesium navigation controls. The **Origin** button zooms into the working area.
5. Zooming below an altitude of 60 km changes the globe to a 2D map; zooming above 90 km returns to 3D. Tools and the sidebar remain accessible. Editing pauses during the animation.
6. Save projects as `.algetric` files. Ctrl+S saves, Ctrl+O opens, Ctrl+N creates a project, Ctrl+Z undoes, Ctrl+Y redoes, and Delete removes the selection. When leaving a modified project, you can save, discard changes, or cancel.

Local coordinates use meters in an ellipsoidal WGS84 azimuthal equidistant projection, transformed through PROJ. Measurements are **planimetric**, rather than geodesic. Editing is limited to a radius of 50 km from the origin and latitudes within ±80°. The origin becomes fixed after the first object is created, even if all objects are subsequently deleted. The globe remains navigable worldwide. Self-intersecting or degenerate polygons and invalid coordinates are rejected without replacing the last valid state.

## Maps and offline use

Under **Map → Map provider**, configure an HTTPS raster XYZ server with `{z}`, `{x}`, and `{y}` placeholders, supported zoom levels, and attribution. Use your own server or a contracted provider that authorizes the intended use. No public server or paid service is preconfigured. Credentials must not be included in the URL; an optional Bearer token is stored in Linux Secret Service, separately from the project.

To download regions, enable the provider's offline permission and open **Map → Offline regions**. Enter a name, west/east/south/north bounds in degrees, and zoom levels. The size estimate assumes 40 KB per tile; actual storage depends on the images. Each region is limited to 50,000 tiles. Regions cannot cross the antimeridian.

Downloads can be interrupted and resumed from the region list, including after restarting the application. Stored tiles are not downloaded again. Downloads use up to three simultaneous requests, four attempts for HTTP 429/5xx responses, and respect `Retry-After`. Network or disk failures preserve committed tiles. Removing a region preserves tiles shared with other regions.

The map reads from the local SQLite store first, then requests missing tiles from the server. Online navigation does not automatically create persistent downloads. Without internet access, downloaded regions and geometries remain available; areas without tiles show a neutral background and a notice. Globe assets are local and do not require a network connection.

Configuration and maps are stored in `~/.local/share/algetric`, or the data directory returned by the operating system. Projects contain only the origin, geometries, layers, styles, and view state. Maps and credentials are not included in project files.

## Verification and distribution

```sh
python3 -m unittest discover -s tests/installation -v
dotnet test tests/Algetric.Tests
npm test --prefix src/Algetric.Desktop/Web
dotnet run --project tests/Algetric.Smoke
dotnet run --project src/Algetric.Desktop -- --diagnostics
bash scripts/publish-linux.sh
```

The smoke test opens a real window and checks WebGL, synchronization, globe/map transitions, geographic editing, history, and persistence. It writes results to `artifacts/smoke`. In a headless environment, use `dbus-run-session -- xvfb-run -a` with Mesa. Unit tests use simulated HTTP providers and do not access external map services.

The final package is written to `artifacts/algetric-linux-x64.tar.gz`. Extract it and run `bash install.sh` to add it to your applications menu, or run `./Algetric.Desktop` directly. The package includes the .NET runtime; PROJ and WebKitGTK remain native system dependencies. See the [architecture](docs/architecture.md), [verification](docs/verification.md), and [license notices](THIRD_PARTY_NOTICES.md).

Coverage algorithms, obstacle avoidance, 3D terrain, polygons with holes, and advanced GIS formats are outside the scope of this MVP.
