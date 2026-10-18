# Verification and acceptance

## Automated tests

The .NET suite covers invalid polygons, measurements, atomic drag edits, history, coordinate bounds, PROJ against a known reference, and coordinate round trips with less than 1 cm of error. It also covers saving and reopening projects, preserving an existing valid file after a failed save, persistent origin locking, and replayed or stale messages.

Offline tests simulate HTTP 429 responses, interrupted downloads and resumption after reopening the cache, insufficient disk space, removal of regions with shared tiles, and serving local tiles while the network is unavailable. Insecure providers and URLs containing credentials are rejected. Public providers can work without secret-tool; storing a credential requires the service.

JavaScript tests verify distinct transition thresholds and rejection of stale snapshots. The smoke test uses a real WebView: it opens the globe, checks WebGL, renders a polygon, zooms into 2D, loads a local SQLite raster tile, creates a point through a Cesium pointer event, undoes and redoes the edit, zooms back out to 3D, and saves and reopens the project. The Cartesian canvas is also rendered to PNG for visual inspection.

## Object properties checks

Run `dotnet run --project tests/Algetric.Smoke -- --properties-only` in a graphical session to check the compact sidebar. This checks that name/vertex and X/Y share their respective rows, Enter and focus loss save edits, moving between fields preserves focus, repeated confirmation creates only one undo operation, renaming works without an Apply button, invalid coordinates preserve the geometry, edits target the selected vertex, and inline deletion removes the intended object and supports undo. The same scenario checks the Move toolbar button, panning with the left mouse button, temporary movement while Space is held (including key repeat), restoration on release or window deactivation, preservation of an unfinished segment, and ignoring Space in text inputs. It writes `artifacts/smoke/cartesian.png` for visual inspection. These checks also run before the full WebView smoke scenario.

## Layer panel checks

The editor smoke scenario checks the **+** button in the layer heading, indented object rows, active-layer selection, and creation in that layer. It simulates pointer press/movement/release to verify that a dragged row disappears immediately, a floating preview follows the pointer, dropping on another layer moves the object, and undo/redo preserves its identity and selection. Dropping outside the layer list or onto the original layer leaves the document and history untouched. Escape and loss of pointer capture cancel the drag. The drag preview is rendered to `artifacts/smoke/layer-drag.png` for inspection.

Domain tests also reject nonexistent objects/layers, avoid history entries for unchanged destinations, and verify that selecting an active layer does not mark the project modified.

## Desktop installation tests

Run `python3 -m unittest discover -s tests/installation -v`. Tests install a simulated package into temporary user directories, validate the desktop entry, and launch it through `gio` when available. They cover paths with spaces and special characters, independence from the original package directory, upgrades, uninstalling without deleting user data, missing assets, and refusing to overwrite unrelated launchers. The suite contains seven tests, including first-run source setup, argument forwarding, rebuilding an existing source package on each install, replacing an older installed binary, and preserving the installation after a failed publish. Python is needed for these tests only; installation itself uses Bash and standard Linux utilities. The real published package was also installed into temporary user directories, checked with `desktop-file-validate` and `--diagnostics`, and uninstalled while preserving user data.

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

## Recorded initial MVP result

All 26 .NET tests and 2 JavaScript tests passed. The publishing script completed without compiler warnings. The smoke test covering WebGL, offline tiles, and transitions in both directions passed on Arch Linux and on Ubuntu 24.04 without networking. The self-contained binary also confirmed Cesium assets, WebKitGTK, and PROJ through the `--diagnostics` command.

## Windows verification

Run `pwsh -File tests/windows/Update.Tests.ps1` on any host with PowerShell to verify that source installations always republish, extracted packages need no build, failed publishing stops installation, and registered custom installation directories are reused unless explicitly overridden. These checks use temporary fixtures and a simulated registration.

Run `pwsh -File tests/windows/Packaging.Tests.ps1` on any host with PowerShell to validate script syntax, ZIP contents, native dependencies and license notices, checksums, and rejection of a missing coordinate database. The package tests use controlled native fixtures; they do not execute Windows binaries.

On a Windows machine with WebView2, build with `scripts/publish-windows.ps1`, then run `tests/windows/Installation.Tests.ps1`. It verifies Start menu shortcuts, Installed apps registration, installed-binary diagnostics, discovery and upgrade of a custom installation without repeating its directory, copying new package files and removing obsolete files, preservation of desktop shortcuts and user data, uninstall, and rejection of unrelated directories. It requires a profile without an existing Algetric installation. The Windows-only .NET test verifies DPAPI round trips, absence of plaintext tokens, and corrupted credential rejection.

The Windows CI job builds pinned native dependencies, runs these checks, and exercises the WebView2 smoke test. Its software-rendering browser flags apply only to the CI smoke process. Native Windows installation, DPAPI, and WebView2 execution cannot be validated from the Linux development host; the Windows runner must confirm them before release.

For the Windows implementation, the Linux host passed 27 .NET tests (the DPAPI test was skipped), all five Linux installation/source-launcher tests, the portable PowerShell packaging checks, a self-contained win-x64 cross-publish, and the full Linux WebView smoke test with the Kestrel server. Native Windows execution remains pending the Windows CI job.
