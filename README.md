# Polymita 0.8.6

Favorites, configured component recipes and canvas tools for **Grasshopper in Rhino 8 on Windows**.

## Changes in 0.8.6

Ordinary clicks retain native delayed drag activation. Interrupted drags release correctly and retain Undo. Mouse and keyboard releases schedule a full Canvas repaint so wire colours follow current selection. Multiple selections snap using the grabbed component's bounds and ports. Group-border drags snap using the group bounds; nested groups and annotations are supported.

This release passed 26 new native Canvas checks and 48 prior-release regression checks inside Rhino, in addition to 149 local checks. The 48 native checks include the 16 local geometry cases. See `CAMBIOS-0.8.6.md` and `docs/test-results/0.8.6/` for the detailed scope and results.

## Changes retained from 0.8.5

- **Native drag + Alt.** Ordinary alignment now extends Grasshopper's own drag interaction. Pressing Alt during a drag delegates that gesture to Grasshopper, including its copy preview, Alt repeat/toggling, mouse release, cancellation, Undo and Redo. The plugin has no separate Alt-drag duplication routine. Start dragging, then tap Alt; pressing Alt before mouse-down keeps whatever gesture Grasshopper assigns to it.
- **Port adhesion.** Edges and object centres have an 8-screen-pixel snap radius. Input/output port alignments have a 32-screen-pixel radius on both axes, at any canvas zoom. A port within that radius takes precedence over an edge. Moving outside it releases that alignment.
- **Nearest connected wire.** The mouse-down location selects the closest connected input/output grip of the component being grabbed. Only the other endpoints of that grip's actual wires are horizontal alignment targets. The choice stays fixed for the gesture. An unused port cannot steal a connected wire's anchor. With a selection of several objects, the grabbed component determines the wire; on a group border, the group outline supplies the snap without port targets. With no external wire, the grabbed component's centre axes align to stationary input/output vertices.
- **Operation icons removed.** The connect and duplicate drawings and image assets are removed. Commands and shortcuts remain. Existing operation favorites are preserved as full-width text rows; newly created libraries no longer get an automatic Operations section.
- **One current name.** Assembly metadata, namespace, project, dialogs, exports, recipe chunk names and runtime extraction use Polymita. Settings are saved in `%APPDATA%\Grasshopper\Polymita`, as `library.polymita.json` and `toolbox.json`.
- **Migration without losing data.** Existing libraries/settings are copied to the new location when the corresponding Polymita file does not already exist. IDs, user names, recipe XML, preferred ports, ordering, shortcuts and backups are preserved. Previous directories remain as backups and are never used for new writes. Old spellings exist only in migration/installer compatibility inputs and the tests of those inputs.

The stronger Polymita snaps apply to ordinary dragging. After Alt is pressed, that gesture uses Grasshopper's native dragging and snapping until mouse release; the next ordinary drag again uses Polymita alignment.

## Installation

1. Close **all** Rhino windows.
2. Extract the complete ZIP.
3. Run `Install-Polymita.ps1` in PowerShell. It archives earlier known plugin assemblies and installs `release/Polymita.gha` into `%APPDATA%\Grasshopper\Libraries\Polymita`.
4. Restart Rhino and Grasshopper.

Manual installation: place `release/Polymita.gha` in that folder, replacing the previous build. Keep only one active build of the plugin in Libraries, including its subfolders. The single GHA embeds the three required Harmony runtimes. The installer does not touch your saved definitions.

## Existing functionality

### Favorites and recipes

Drag a wire from an input or output and release it over empty canvas or a group to open the palette. Click a favorite to insert it; right-click to choose a port or insert without a wire. You can also click a port and then the canvas. Double Shift opens favorites at the pointer without an existing wire. Search preserves section and item order, without filtering favorites by wire data type.

The library editor uses a draft until **Save**. Add/reorder/rename sections, components and text operations; move favorites between sections; import/export libraries; capture or update a configured component recipe. Recipes retain native component configuration while removing external connections from the saved copy. Import accepts ordinary JSON, including older exported libraries. New exports use `.polymita.json`.

### Canvas tools

- Ctrl + left drag on background cuts crossed visible wires, with feedback along the stroke. Ctrl-click on objects preserves native multiselection. One Undo restores the entire stroke; Escape cancels it.
- Shift during ordinary dragging constrains movement to horizontal or vertical. Selections and nested groups translate together. Undo/Redo and Escape preserve original placement.
- Alignment guides: green for edges, red for centre-to-centre, blue for input/output vertices.
- Adaptive rounded polylines and orthogonal wire styles, native hit testing and optional selected-wire highlighting.
- Component labels, optional nicknames and large group labels at low zoom.
- Component finder and profiler, with navigation to results.
- A native Rhino viewport pane, view/display-mode choices, native selection and commands.
- Containers before/after selections, selection connections, duplication beside the original, and adding overlapping objects to groups.

### Default shortcuts

| Action | Shortcut |
|---|---|
| Native copy during a drag | Start dragging, then tap Alt |
| Connect selection | Alt+W |
| Duplicate selection beside original | Alt+Q |
| Data containers before / after selection | Alt+A / Alt+D |
| Add overlapping objects to groups | Alt+G |
| Open favorites at pointer | Double Shift |
| Open favorites at canvas centre | Ctrl+Space |
| Library editor | Ctrl+Shift+B |
| Save selected component as recipe | Ctrl+Shift+R |
| Rhino viewport | Ctrl+Shift+V |
| Wire tools | Ctrl+Shift+W |
| Labels | Ctrl+Shift+L |
| Finder / profiler | Ctrl+Shift+F |

Commands can be rebound under **Polymita → Customise shortcuts**. Keyboard hooks do not run commands while a canvas interaction or mouse gesture is active.

## Storage and compatibility

Library and tool saves use atomic replacement and `.bak` backups. Existing Polymita data always wins over migration inputs. If multiple old data folders exist and no new library exists, the most recently modified old library is used; settings from the same folder are preferred. Original files are retained for recovery. The plugin's assembly GUID and library schema version are unchanged. A namespace change does not alter the JSON schema, and old recipe XML remains readable.

## Build and verification

Run `build.ps1` with Rhino 8 installed. It creates `dist/Polymita.gha`; Rhino/Grasshopper assemblies are referenced from the local installation and are not redistributed. `Polymita.csproj` provides the equivalent MSBuild project.

```powershell
.\build.ps1
.\tests\run-core.ps1
.\tests\run-storage.ps1
.\tests\run-toolbox.ps1
.\tests\prepare-runtime.ps1 -Name Polymita.CanvasTests
.\tests\run-geometry.ps1
```

Inside Rhino, run `tests/run-regression-086.py` with RunPythonScript for the current native Canvas and Alt/port regression suites. It creates an isolated Canvas and temporary documents. See `CAMBIOS-0.8.6.md` for the Spanish review and validation scope.

## Source map

| File | Responsibility |
|---|---|
| `Plugin.cs`, `Commands.cs`, `ShiftFilter.cs` | Initialization, menu, toolbar and keyboard shortcuts |
| `Palette.cs`, `Editor.cs`, `GridLayout.cs` | Favorites palette, draft library editor and layout |
| `Library.cs`, `SettingsStorage.cs`, `ToolboxSettings.cs` | JSON data, migration and settings |
| `Recipes.cs`, `Insertion.cs`, `BuiltInCatalog.cs` | Native recipes, insertion and component catalog |
| `CanvasGestures.cs`, `DragAnchors.cs` | Wire cutting, alignment, native Alt delegation and wire selection |
| `CanvasOperations.cs` | Explicit selection operations and their Undo records |
| `WireStyles.cs`, `CanvasLabels.cs`, `ComponentFinder.cs` | Wire drawing, labels and finder/profiler |
| `Toolbox.cs`, `RhinoViewportPane.cs`, `NativeRhinoViewHost.cs` | Native Rhino viewport and tool UI |
| `Brand.cs`, `Ui.cs` | Icons and shared appearance |

## License

GPL-3.0-or-later. See `COPYING` and `THIRD_PARTY_NOTICES.md`. Third-party attribution and licenses are included.
