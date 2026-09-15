# Polymita 0.7.0 validation

Rhino 8.34 / Windows. Built against the installed Grasshopper and RhinoCommon SDK.

- 25 core persistence, shortcut and palette checks passed.
- 47 toolbox, wire geometry and migration checks passed.
- Native gesture checks cover sweep intersection, source ordering, multiple wires, Undo/Redo, both polyline variants, hidden wires, nested group movement, cancellation and unchanged surrounding components.
- Desktop check: dragged a parameter near another parameter's center; alignment occurred and Ctrl+Z restored its previous position.
- Ctrl-left message routing is tested inside Rhino against the real canvas. The desktop automation API cannot hold Ctrl across a mouse drag, so the complete physical Ctrl-drag gesture has not been automated end to end.

Existing user document and canvas view are restored after testing. Library data and settings retain their historical location and identifiers.
