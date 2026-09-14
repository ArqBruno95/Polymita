# Polymita 0.7.0

A toolbox for clear, fast work in **Grasshopper for Rhino 8 on Windows**.

Polymita is the scientific name used internationally for Cuba's painted snails. The identity uses a geometric spiral shell, shell yellow, coral, cream and dark ink. English common name: **Cuban painted snail**. [Natural-history reference](https://www.amnh.org/explore/news-blogs/cuba-painted-snails).

## Changes in 0.8.2

- A **click** on an input or output arms the wire and leaves it on the cursor, so a connection can be made with two clicks instead of one held drag.
- **Double-clicking a wire** creates exactly one relay. A fast run of clicks can deliver more than one double-click, and the new relay lands under the cursor where the next one would dissolve it, so one gesture is taken per place and moment. The reach onto the wire is also wider.
- Both **segmented wire styles** work again. The Harmony runtime carried inside the assembly is now unpacked beside the settings and loaded from there; loaded straight from memory it had no file to work from and the patch failed, which left only Grasshopper's own wires available.

## Changes in 0.8.1

- The **Polymita menu** lists the five tools that have a toolbar icon first, in toolbar order, then the switches, then the commands, with no separators. Rhino viewport, Wire style and color and Component and group labels now have shortcuts of their own.
- **Customise shortcuts…** rebinds every command in the plug-in. Select a command, press the keys; Backspace clears one, and taking a combination frees whoever held it. Bindings are stored in `toolbox.json`.
- Snapping a capsule with several outputs measures against the **grip the drag was started nearest** rather than the centre line, which none of several outputs sits on.
- Releasing a dragged wire near **any** port, input or output, completes the connection instead of opening the palette. The search is measured in screen pixels, so it does not shrink as the canvas zooms out.

## Changes in 0.8.0

- **Adaptive wires are filleted.** The two corners are rounded. When the input sits left of the output the stub is a short fixed length, which turns the wire into a straight run with a tight rounded hook at each end.
- **Alt+A / Alt+D** insert a generic **Data** container before or after the selection. Each existing wire on that side gets its own container spliced into it, the way a relay sits in a wire; a port with no wire gets a container joined to it. Containers are drawn as their name rather than their icon.
- **Alt+G** adds to every group each object whose area overlaps it. With nothing selected it tidies the whole definition; select groups to narrow it to those.
- **Double-click a wire** to drop a relay into it. The relay takes the wire over rather than branching off it, so the route stays one chain. **Double-click a relay** to dissolve it and reconnect both ends.
- **Shift** while dragging constrains the move to horizontal or vertical. **Alt** leaves a copy behind; it is read while the drag runs, so it can be pressed before or part-way through.
- Snapping now also aligns the moving centre with the grips of components the selection is wired to, and reaches four times as far on the horizontal centre axis, which is the alignment a drag is usually after. Guides are coloured: green for an edge, red for a centre axis, blue for a connected port.
- **Alt+Q** places the copy immediately beside the original rather than hunting for clear space beyond every obstacle.
- Component captions are on by default and are painted behind objects, so an overlapping component stays readable.
- Favorites inserted from the palette carry **full input and output names**. Each port is named after itself, so the full name shows whichever way the machine-wide Draw Full Names setting is left, and nothing else on the canvas changes.
- The library editor shows each favorite's component icon beside its name.
- Releasing a dragged wire over a group opens the palette, as releasing over bare canvas already did.

## Changes in 0.7.0

- **Cut wires:** hold Ctrl, press the left mouse button on canvas background, and sweep across connected wires. Only Ctrl and the left button are needed, and the stroke continues for as long as that button is held, whether or not Ctrl stays down. The swept path is drawn on the canvas while the stroke is live. Release the button to finish. A single Undo restores the whole stroke, including input source order. Escape cancels the stroke. Hidden wires are preserved. Ctrl-click on a component retains native multiselection.
- **Align while dragging:** components and complete groups snap to nearby edges and centers with horizontal/vertical guides. The selection moves together; nested group contents retain their relative positions. The snap radius is 8 screen pixels at any zoom. Hold **Alt** during the drag to suspend snapping. Undo/Redo restores the whole move; Escape cancels.
- The Polymita menu has **Cut wires · Ctrl + left drag** and **Snap to edges and centers · hold Alt to suspend** switches. Both start enabled and are stored in `toolbox.json`, so they survive a restart. Selection containing sketches or other non-component objects keeps native dragging.

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

Five icons beside Grasshopper's Sketch pencil open the viewport, favorite-library editor, wire settings, label settings and **Find / Profiler**, whose magnifier carries the painted-snail palette. Tools open only when called. The viewport starts hidden. Find / Profiler is also in the menu and opens with **Ctrl+Shift+F**. It locates components and shows native processor timings after recomputing the definition.

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

Run `build.ps1` with Rhino 8 installed to produce `Polymita.gha`. The script uses Windows' .NET Framework compiler and local Rhino references. `WireShelf.csproj` can also build with the .NET 4.8 targeting pack. Run `tests/run-core.ps1` and `tests/run-toolbox.ps1`. `tests/run-gestures.ps1` builds the canvas-gesture suite, which is then executed with `tests/run-gestures.py` from Rhino's RunPythonScript. Native integration tests require Rhino.

GPL-3.0-or-later. Source and license texts are included. See `THIRD_PARTY_NOTICES.md` for QuickConnection, WiresRenderer, Sunglasses, Harmony and McNeel attribution.


