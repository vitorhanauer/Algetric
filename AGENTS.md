# Overview

The goal of this project is to develop a desktop application for working with polygons and lines, similar to GeoGebra. In addition to manipulating these objects in a 2D space, users should be able to view them on the Earth's surface. The application should include a 3D globe; as the user zooms in, the view should expand to fill the screen and transition to 2D. Use the application image in the `mockups` folder as a reference.

# Technologies

The project should be developed on the .NET platform. The following libraries are recommended:

| Technology | Role in the project | Notes |
|---|---|---|
| **Avalonia UI** | Desktop interface in C#/XAML | Preferred choice for the application, especially for Linux support. |
| **NetTopologySuite (NTS)** | 2D computational geometry | Primary library for representing and processing polygons. |
| **Mapsui** | 2D map visualization | Integrates well with Avalonia and NTS geometries. |
| **Avalonia Drawing or SkiaSharp** | Cartesian plane and animations | Useful for building GeoGebra-style interactions. |
| **CesiumJS in a WebView** | 3D globe | Preferred option for the globe. |
| **GDAL/OGR and OSR** | GIS files and coordinate transformations | Add when broader format and projection support is needed. |
| **Clipper2** | Polygon clipping and offsetting | Supplementary library for specific needs. |

Other libraries may be used when needed.

# Coding Practices

- Use clear names for variables and functions; avoid generic names.
- Keep functions small and focused on a single responsibility. Use 25 lines as a guideline during review.
- Design classes with a single responsibility and strong cohesion.
- Write comments that explain why something was done. Avoid obvious comments, such as `a++ // adds one to a`.
- Use Conventional Commits and atomic commits. Each commit should represent one logical, coherent change.
- Use TDD for testable behavior, following Red → Green → Refactor. Run the test before implementation and confirm that it fails because the expected behavior is missing. Implement what is needed to make it pass, then refactor while keeping the tests green.
