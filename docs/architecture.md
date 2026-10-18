# Architecture

- **Core:** immutable documents, layers, NTS geometries, editing commands, history snapshots, and contracts for persistence, coordinate transformations, and maps. It does not depend on Avalonia, storage, or JavaScript.
- **Infrastructure:** native PROJ adapter, versioned JSON with atomic writes, SQLite tile and region storage, bounded downloads, settings, Linux Secret Service or Windows DPAPI integration, and a Kestrel loopback HTTP server.
- **Desktop:** shared ViewModel, Avalonia controls, Cartesian canvas, dialogs, and Cesium in a NativeWebView. Both views modify the document through the same editor.

## Editing flow

Canvas events are converted from pixels to meters. The map emits WGS84 positions, which PROJ transforms into meters. Both paths validate bounds and geometries before recording a new snapshot. Drags are committed only when the pointer is released; previews do not modify the document.

The document is the source of truth. C# projects visible objects into WGS84 and sends their state to Cesium. Messages include a session, sequence number, and revision. Replayed messages, messages from an old session, and messages with a stale revision do not modify the document. Switching projects renews the session. Selection does not create a history entry.

The bridge uses typed JSON over loopback HTTP and updates through `NativeWebView.InvokeScript`. Kestrel binds a user-owned socket on both platforms, avoiding Windows HTTP.sys URL ACL requirements. The server listens only on 127.0.0.1, requires a random path for each execution, and rejects foreign origins. The temporary URL and static assets contain no credentials. Browser tile URLs point to the loopback server; provider authentication happens in C#.

## Persistence

Version 1 of `ProjectDocument` contains the origin, its persistent lock, layers, objects, and `ViewState`. Geometries store local vertices and their type; polygons do not repeat the first vertex at the end. The closing coordinate is added only when constructing the NTS geometry. Identifiers survive saving and history operations.

Saving validates the document and transforms its coordinates before serialization. It writes a temporary file beside the destination, flushes it to disk, and replaces the final file. Unknown versions, invalid references, invalid geometries, and malformed content are rejected.

The cache uses (provider, zoom, x, y) keys, region records, and associations between regions and tiles. A tile can belong to multiple regions. Tile writes and region associations use a transaction; region metadata uses UPSERT to preserve associations when a download resumes. Network operations do not block the UI.

## Evolution

There are no migrations from earlier documents because this is the first version. Future versions must introduce explicit migrations before changing the format. GIS formats, alternative renderers, and algorithms can build on the existing contracts without introducing UI dependencies into the domain.

## Windows distribution

The native adapter resolves Linux's `libproj.so.25` or a bundled Windows PROJ DLL. Windows contexts explicitly load `proj-data/proj.db` from the application directory. The vcpkg manifest pins the native dependency baseline; release DLLs, database, and copyright notices are packaged with the self-contained application. `AlgetricProjRoot` supplies these assets to desktop, test, and smoke builds.

The Windows installer uses per-user application storage, Start menu shortcuts, and an HKCU uninstall registration. Its ownership marker prevents replacing or deleting unrelated installation directories. User data lives outside the installation directory. DPAPI encrypts provider tokens for the current Windows user. WebView2 detection follows Microsoft's Evergreen registry locations.
