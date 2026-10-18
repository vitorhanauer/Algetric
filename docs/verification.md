# Verification and acceptance

## Automated tests

The .NET suite covers invalid polygons, measurements, atomic drag edits, history, coordinate bounds, PROJ against a known reference, and coordinate round trips with less than 1 cm of error. It also covers saving and reopening projects, preserving an existing valid file after a failed save, persistent origin locking, and replayed or stale messages.

Offline tests simulate HTTP 429 responses, interrupted downloads and resumption after reopening the cache, insufficient disk space, removal of regions with shared tiles, and serving local tiles while the network is unavailable. Insecure providers and URLs containing credentials are rejected. Public providers can work without secret-tool; storing a credential requires the service.

JavaScript tests verify distinct transition thresholds and rejection of stale snapshots. The smoke test uses a real WebView: it opens the globe, checks WebGL, renders a polygon, zooms into 2D, loads a local SQLite raster tile, creates a point through a Cesium pointer event, undoes and redoes the edit, zooms back out to 3D, and saves and reopens the project. The Cartesian canvas is also rendered to PNG for visual inspection.

## Desktop installation tests

Run `python3 -m unittest discover -s tests/installation -v`. Tests install a simulated package into temporary user directories, validate the desktop entry, and launch it through `gio` when available. They cover paths with spaces and special characters, independence from the original package directory, upgrades, uninstalling without deleting user data, missing assets, and refusing to overwrite unrelated launchers. The suite contains five tests, including first-run source setup and argument forwarding. Python is needed for these tests only; installation itself uses Bash and standard Linux utilities. The real published package was also installed into temporary user directories, checked with `desktop-file-validate` and `--diagnostics`, and uninstalled while preserving user data.

## Reproduce in an isolated Ubuntu environment

After preparing the Cesium assets:

```sh
dotnet publish tests/Algetric.Smoke -c Release -r linux-x64 --self-contained true -o artifacts/smoke-linux-x64
docker build -f scripts/Dockerfile.ubuntu-smoke -t algetric-ubuntu-smoke .
docker run --rm --network none --shm-size=512m algetric-ubuntu-smoke
```

The container uses Ubuntu 24.04, Mesa, Xvfb, and WebKitGTK. It runs as an unprivileged user without a system .NET installation. Networking remains disabled during the test. The published application supplies the .NET runtime.

## Environments

- Arch Linux, KDE Wayland session with Avalonia/WebKitGTK through XWayland: the application was run and the Cartesian layout was inspected.
- Ubuntu 24.04 container, X11/Xvfb with Mesa and networking disabled: the WebView smoke test was run.
- Native WPE/Wayland backend: not exercised; the validated integration uses WebKitGTK.

The CI configuration reproduces the build, tests, and smoke test on Ubuntu 24.04 and generates the Linux x64 package. GitHub must run the remote workflow after the repository is pushed.

No real external provider was configured. Downloads, resumption, and network failures are verified with controlled HTTP fixtures. Production server behavior and performance must be checked with the provider chosen by the user. The MVP does not include 3D terrain coverage or a trajectory generation algorithm.

## Recorded result

All 26 .NET tests and 2 JavaScript tests passed. The publishing script completed without compiler warnings. The smoke test covering WebGL, offline tiles, and transitions in both directions passed on Arch Linux and on Ubuntu 24.04 without networking. The self-contained binary also confirmed Cesium assets, WebKitGTK, and PROJ through the `--diagnostics` command.
