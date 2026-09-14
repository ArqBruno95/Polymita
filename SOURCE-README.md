# Polymita source package

This archive contains the editable source for Polymita 0.7.0, a Grasshopper plug-in for Rhino 8 on Windows.

## Build

1. Install Rhino 8 with Grasshopper.
2. Open PowerShell in the extracted folder.
3. Run `./build.ps1 -OutputDirectory ./dist`.

The build script uses the .NET Framework compiler included with Windows and references the Rhino 8 SDK from `C:\Program Files\Rhino 8`. To use another Rhino installation, pass `-RhinoRoot`.

The resulting `dist/Polymita.gha` and `dist/runtimes` folder can be copied to `%APPDATA%\Grasshopper\Libraries\Polymita` after closing Rhino. The existing `WireShelf` settings folder is intentionally preserved by the plug-in.

`WireShelf.csproj` contains the equivalent MSBuild project for the .NET Framework 4.8 targeting pack. Rhino and Grasshopper assemblies are referenced from the configured `RhinoRoot` and are not redistributed here.

## Tests

Run `./tests/run-core.ps1` and `./tests/run-toolbox.ps1` for the standalone checks. The scripts compile against the installed Rhino 8 SDK. The files named `*preview.py`, `*native*.py`, and `run-gestures.py` are Rhino-side integration helpers and should be run from Rhino's `RunPythonScript` command when testing UI behavior.

## Source layout

- `src/` — plug-in implementation, toolbox UI, viewport host, wire rendering and canvas gestures.
- `tests/` — persistence, catalog, rendering, operation and gesture tests.
- `docs/` — feature and validation notes.
- `lib/harmony-2.3.3/lib/` — the three Harmony runtime assemblies used by the reversible wire-rendering patch.

The project retains the `WireShelf` namespace, settings path and assembly GUID for compatibility with existing Polymita libraries and user settings.
