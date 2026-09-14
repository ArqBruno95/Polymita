# Polymita 0.7.0

A toolbox for clear, fast work in **Grasshopper for Rhino 8 on Windows**.

Polymita is the scientific name used internationally for Cuba's painted snails. The identity uses a geometric spiral shell, shell yellow, coral, cream and dark ink. English common name: **Cuban painted snail**. [Natural-history reference](https://www.amnh.org/explore/news-blogs/cuba-painted-snails).

## Changes in 0.7.0

- **Cut wires:** hold Ctrl, press the left mouse button on canvas background, and sweep across connected wires. Release to finish. A single Undo restores the whole stroke, including input source order. Escape cancels the stroke. Hidden wires are preserved. Ctrl-click on a component retains native multiselection.
- **Align while dragging:** components and complete groups snap to nearby edges and centers with horizontal/vertical guides. The selection moves together; nested group contents retain their relative positions. The snap radius is 8 screen pixels at any zoom. Undo/Redo restores the whole move; Escape cancels.
- The Polymita menu has **Cut wires · Ctrl + left drag** and **Snap to edges and centers** switches. Both start enabled; switches apply for the current session. Selection containing sketches or other non-component objects keeps native dragging.

## Changes in 0.6.0

- A real Rhino model view replaces the preview-only control. Select Rhino objects, use the gumball, zoom, and create geometry with native command previews.
- Enter a command such as `_Line` in the field below the viewport, press Enter or **Run**, then pick points directly in the view. The prompt updates with the command's options. The field also accepts coordinates and options; Escape cancels. **Fit** frames the model and GH preview. Use `_Zoom _Selected` to focus selected objects.
- The viewport follows Rhino's native mouse/navigation preferences. Rhino commands and Undo operate on the active Rhino document.
- **View** and **Display** selectors use an automatically sized header. Commands, prompts and buttons have separate rows and margins, including at narrow panel widths. Drag the divider to resize.
- Component titles are transparent italic text using the native Grasshopper font and canvas transform. They now scale with zoom just like input/output captions. Large group titles retain their separate zoom-out behavior.
- New Polymita snail icon, simplified T icon for labels, and painted-snail palette throughout the toolbox.

The interactive viewport hosts a complete Rhino floating frame aligned to the Grasshopper panel; it retains Rhino's native object, command and display behavior. Only objects belonging to Rhino can be edited. Grasshopper preview geometry must be baked before Rhino commands can modify it. Dialog-based commands may still open their own Rhino windows. This Windows prototype does not replace Rhino's full command history or toolbars.

## Install or update

Close Rhino, extract the whole ZIP and run `Install-Polymita.ps1` in PowerShell. The installer archives known WireShelf, Nítido and Zunzún assemblies and preserves your existing library/settings. Restart Rhino and Grasshopper.

Manual installation: copy `dist/Polymita.gha` and `dist/runtimes` into `%APPDATA%\Grasshopper\Libraries\Polymita`. Archive the old plugin assembly first: the versions share one plugin GUID and must not load simultaneously.

## Toolbar and tools

Four icons beside Grasshopper's Sketch pencil open the viewport, favorite-library editor, wire settings and label settings. Tools open only when called. The viewport starts hidden. **Find / Profiler** is in the menu and opens with **Ctrl+Shift+F**. It locates components and shows native processor timings after recomputing the definition.

## Favorites and recipes

Drag a wire from an input or output and release over empty canvas, or click a port and then empty canvas. The same icon palette appears for every data type. Click an icon to insert/connect, or right-click for a different port or an insertion without a wire. Hover to see its name.

**Double Shift** opens the palette at the cursor without a wire. **Ctrl+Space** opens it in the center. Arrow keys and Enter navigate/insert; Esc closes it.

The supplied catalog contains the 197 requested entries plus earlier recipes and two operations: 205 shortcuts in the original user's library. The palette adapts its width to fit the screen and allows scrolling for larger libraries. Third-party components require their plugins; a question mark means the component is missing.

The library editor works on a draft: click **Save** to apply edits. Use **+ Section**, **+ Component**, **+ Operation**, Rename, ↑/↓, Move to and Delete. Import adds sections without replacing yours; Export saves a portable `.wireshelf.json` library.

To save a configured component, select it and choose **Capture selection** or **Ctrl+Shift+R**. Native serialization preserves persistent data, panel text, Flatten/Simplify options, nicknames and supported component settings. External wires are omitted from recipes. **Update recipe** replaces a favorite's configuration from a selected instance of the same type. Recipes capture one object at a time.

## Selection operations

- **Alt+W:** connect selected components from left to right. The rightmost object receives outputs in component/port order. A single input receives all outputs; multiple inputs are matched by index until either list is exhausted. Existing wires remain and duplicates are skipped. Ambiguous rightmost positions and detected cycles are rejected.
- **Alt+Q:** duplicate selected components and complete nested groups. Preserve data, settings, group membership, internal wires and incoming external connections. Copies receive new IDs and move to free space on the right while keeping their vertical arrangement.

Both operations support one-step **Undo/Redo**. Keep Grasshopper active and the pointer over the canvas, outside text editors. Operations also appear in the menu and can be added to any favorites section.

## Compatibility and data

The storage directory remains `Grasshopper/WireShelf` so existing libraries and recipes remain available. `library.wireshelf.json` and `toolbox.json` use atomic saves with `.bak` backups. English migration runs once and translates known shipped labels; it preserves custom names, recipe payloads, IDs and ordering. Historical namespace and plugin GUID are retained for compatibility. Old angle/font-size settings remain readable but no longer control adaptive routing or component label size.

Disable QuickConnection if it intercepts the same wire gesture. Other plugins can draw additional names/wires; Polymita does not change their settings. Native Windows dialogs and third-party component names follow their own language settings.

This prototype is tested in Rhino 8.34, Windows, .NET 8.0.14. Not all third-party serialization or large-definition performance has been verified. Duplication temporarily serializes the definition and may take time for large files.

## Build and license

Run `build.ps1` with Rhino 8 installed to produce `Polymita.gha`. The script uses Windows' .NET Framework compiler and local Rhino references. `WireShelf.csproj` can also build with the .NET 4.8 targeting pack. Run `tests/run-core.ps1` and `tests/run-toolbox.ps1`; native integration tests require Rhino.

GPL-3.0-or-later. Source and license texts are included. See `THIRD_PARTY_NOTICES.md` for QuickConnection, WiresRenderer, Sunglasses, Harmony and McNeel attribution.


