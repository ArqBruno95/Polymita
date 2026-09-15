# Polymita 0.6.0 validation

Tested in Rhino 8.34, Windows, .NET 8.0.14. Production build: no compiler errors or warnings.

- 25 core checks: library persistence/backups, gestures and palette layout.
- 47 toolbox checks: wire paths/hit tests, settings and English library migration.
- 27 visual checks inside Rhino: rasterized label/port font heights at 50%, 100%, 173% and 282% zoom, with 96/120/144 DPI; proportional title scaling; brand/icon dimensions.
- 21 native interaction and layout checks: real object selection, gumball movement, actual line creation, active native viewport, unclipped view/display/command controls at widths 300/420/900, five camera presets and two display styles.

120 automated checks passed. UI testing also observed the provisional Line before the second point, mouse-wheel zoom, resizing the panel and closing/reopening the native viewport. Geometry and the definition created for testing are removed; user data is retained.

Native viewport lifecycle keeps the full floating Rhino frame intact, changing its owner/chrome/position rather than reparenting the rendering child. View creation is deferred until Rhino is idle; closure during a command is deferred until it ends. Document-close events release managed references before Rhino destroys its views.

Not exhaustive: every Rhino/third-party command, every display driver, and every multi-monitor/DPI transition. Grasshopper-only preview objects are not Rhino document objects and require baking before native editing. Existing recipe/insertion/operation implementation is unchanged; its historical native validation is in previous releases.
