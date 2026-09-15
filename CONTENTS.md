# Polymita 0.8.4 — package contents

A Grasshopper plug-in for Rhino 8 on Windows. Everything needed to install it, read
it, rebuild it and test it is in this archive.

## Just want to install it

`release/Polymita.gha` is the whole plug-in in one file — the Harmony runtimes it
needs travel inside it. `release/README.md` has the steps, including the two that
catch people out: archive any older build first, because every version shares one
plug-in GUID, and clear the Windows download block on the file.

## Layout

| Path | What it is |
|---|---|
| `release/Polymita.gha` | Ready to install. Built against the Rhino 8.34.26223.11001 SDK, .NET Framework 4.8. |
| `release/README.md` | Install steps. |
| `src/` | The whole plug-in, 22 C# files. |
| `tests/` | Standalone and Rhino-side test suites, with their runner scripts. |
| `docs/icons/` | The toolbar icons as PNG, at 24 px and 96 px, plus a contact sheet. |
| `docs/` | Feature notes, the requested-shortcut catalogue and validation records. |
| `lib/harmony-2.3.3/` | The three Harmony runtimes the build embeds. |
| `build.ps1` | Builds `Polymita.gha` using Windows' own compiler and a local Rhino. |
| `WireShelf.csproj` | The same build as an MSBuild project. |
| `Install-Polymita.ps1` | Copies the build into the Grasshopper libraries folder. |
| `README.md` | What the plug-in does, feature by feature, newest changes first. |
| `SOURCE-README.md` | How to build and test. |
| `COPYING`, `THIRD_PARTY_NOTICES.md` | GPL-3.0-or-later, and attribution for QuickConnection, WiresRenderer, Sunglasses, Harmony and McNeel. |

## About the icons

The icons are **drawn in code**, in `src/Brand.cs` — there are no image files behind
them, which is why they stay sharp at any DPI. The PNGs in `docs/icons/` are
renderings of those same drawing calls, for use in documentation or mock-ups.
`src/Brand.cs` remains the authoritative version: change it there and the toolbar,
the menu and the library editor all follow.

## Rebuilding

With Rhino 8 installed:

```powershell
./build.ps1 -OutputDirectory ./dist
```

Or without Rhino installed at all, against McNeel's published references — this also
works on macOS and Linux, since it only ever needs the reference assemblies:

```
dotnet build WireShelf.csproj -c Release -p:RhinoNuGetVersion=8.34.26223.11001 -o dist
```

## Tests

`tests/run-core.ps1` runs on its own and covers the library store, search, gesture
timing, palette layout and settings. `tests/run-toolbox.ps1` and
`tests/run-gestures.ps1` build suites that need Rhino: run
`tests/run-gestures.py`, `tests/run-in-rhino.py` and the other `*.py` helpers from
Rhino's `RunPythonScript`.
