# Polymita 0.5.1 validation — 2026-09-13

Rhino 8.34, Windows, .NET 8.0.14. Release compiled without warnings or errors.

29 focused regression checks ran inside Rhino: fixed text metrics at five canvas zoom levels and four monitor DPI values; native font baseline; stale hover position before a new drag; actual camera rotation; invariant orbit target/radius; stopping movement after release.

Interactive canvas inspection at 173%, 125% and 65% confirmed transparent italic titles remain the same screen size. The embedded Top viewport now frames the current Sphere preview rather than displaying an empty view. The active definition and library were retained.

The native SDK mouse handlers were inspected to confirm the inherited control keeps a previous hover position without resetting it on mouse-down. Navigation is now bounded by a captured drag with its own initial position. Shift+right-drag pans instead of rotating. New view directions are framed against the current GH/Rhino geometry after layout.

Labels moved from CanvasPostPaintObjects to CanvasPaintEnd, outside the object image used while zooming. Their font uses the standard GH 8 pt font converted to device pixels with the current monitor DPI, independent of canvas zoom.

Mouse navigation was exercised through the control's real mouse event handlers inside Rhino. Physical right-button dragging is not exercised by the desktop automation tool. This update does not rerun the unrelated recipe/operation suites; their 0.5.0 validation remains in the earlier release.
