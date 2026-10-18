# Third-party components

The application uses the components listed below. Lockfiles also record transitive dependencies.

| Component | License / source |
|---|---|
| .NET | MIT, https://github.com/dotnet/runtime |
| Avalonia and Avalonia.Controls.WebView | MIT, https://github.com/AvaloniaUI/Avalonia |
| Inter | SIL Open Font License 1.1, https://github.com/rsms/inter |
| NetTopologySuite | BSD-3-Clause, https://github.com/NetTopologySuite/NetTopologySuite |
| CesiumJS | Apache-2.0, https://github.com/CesiumGS/cesium |
| Microsoft.Data.Sqlite | MIT, https://github.com/dotnet/efcore |
| SQLitePCLRaw | Apache-2.0, https://github.com/ericsink/SQLitePCL.raw |
| SQLite | Public domain, https://sqlite.org/copyright.html |
| PROJ | MIT, https://proj.org/ |
| WebKitGTK / WPE WebKit | LGPL/BSD and component-specific licenses, https://webkitgtk.org/ |

Cesium and its assets are copied with the notices distributed in the npm package. Renderer credits and provider attribution remain visible on the map. WebKitGTK and PROJ are system dependencies on Linux. Windows packages include PROJ, its database, and native dependencies built with vcpkg; their copyright notices are included under `native-licenses`. The Microsoft Edge WebView2 Runtime is installed separately under Microsoft's license. Map data has its own license, as specified by the provider configured by the user.
