# Polymita 0.7.0 — ready to install

`Polymita.gha` is the whole plug-in in one file. The three Harmony runtimes it needs
are carried inside it, so nothing else has to be copied.

## Install

1. **Close Rhino.**
2. Open `%APPDATA%\Grasshopper\Libraries` (paste that into the Explorer address bar).
3. **Remove or rename any earlier build first** — `WireShelf.gha`, `Nitido.gha`,
   `Zunzun.gha` or an older `Polymita.gha`, including ones inside subfolders. Every
   version shares one plug-in GUID and two of them must never load at the same time.
4. Copy `Polymita.gha` into that folder.
5. Right-click `Polymita.gha` → **Properties** → tick **Unblock** → OK. Windows marks
   files that arrived from the internet and Grasshopper silently refuses to load them.
6. Start Rhino and open Grasshopper. A **Polymita** menu appears in the Grasshopper
   window and four icons appear beside the Sketch pencil.

Your existing library and settings are kept: they live in `%APPDATA%\Grasshopper\WireShelf`
and are not touched by installing or removing the plug-in.

## Built from

Source in this repository, compiled against the Rhino 8.34.26223.11001 SDK, targeting
.NET Framework 4.8. To rebuild it yourself, either run `build.ps1` with Rhino 8 installed,
or build the project against McNeel's published references, which needs no Rhino:

```
dotnet build WireShelf.csproj -c Release -p:RhinoNuGetVersion=8.34.26223.11001 -o dist
```
