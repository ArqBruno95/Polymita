using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Polymita;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// Runs inside Rhino, on an isolated canvas and transient documents. Drives the wire
// interaction directly rather than through the canvas, so the rules that decide
// between holding a wire, joining a port, asking for a favorite and letting
// Grasshopper have the click are all exercised without a real mouse.
public static class WireGestureTests
{
    static TextWriter log; static int count;
    static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); log.WriteLine("PASS " + message); log.Flush(); count++; }

    static T Add<T>(GH_Document doc, T obj, float x, float y) where T : IGH_DocumentObject
    {
        obj.CreateAttributes(); obj.Attributes.Pivot = new PointF(x, y); doc.AddObject(obj, false);
        obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout(); return obj;
    }
    static GH_CanvasMouseEvent At(GH_Canvas canvas, PointF canvasPoint)
    {
        var p = canvas.Viewport.ProjectPoint(canvasPoint);
        return new GH_CanvasMouseEvent(Point.Round(p), canvasPoint, MouseButtons.Left, 1, 0);
    }
    // The wire is always built as if the press had landed on the grip, from outside
    // the capsule, unless a test says otherwise.
    static ShelfWireInteraction Start(GH_Canvas canvas, IGH_Param source, bool fromInput, bool onGrip)
    {
        var grip = fromInput ? source.Attributes.InputGrip : source.Attributes.OutputGrip;
        var top = source.Attributes.GetTopLevel;
        return new ShelfWireInteraction(canvas, At(canvas, grip), source, fromInput, onGrip, top.Bounds);
    }

    public static void Run(string root)
    {
        count = 0;
        var folder = Path.Combine(root, "test-output");
        Directory.CreateDirectory(folder);
        using (log = new StreamWriter(Path.Combine(folder, "wire-gesture.txt"), false))
        using (var canvas = new GH_Canvas())
        {
            canvas.Size = new Size(900, 600); canvas.CreateControl(); canvas.Viewport.Zoom = 1;
            using (var doc = new GH_Document())
            {
                canvas.Document = doc;
                var from = Add(doc, new Param_Number(), 120, 160);
                var to = Add(doc, new Param_Number(), 520, 160);
                var far = new PointF(300, 420);

                // A click on the grip that goes nowhere keeps the wire on the cursor.
                var held = Start(canvas, from, false, true);
                var answer = held.RespondToMouseUp(canvas, At(canvas, from.Attributes.OutputGrip));
                Check(answer == GH_ObjectResponse.Ignore && held.Held,
                    "A click on an output grip leaves the wire riding the cursor");

                // And the next press puts it down on the other component's input.
                answer = held.RespondToMouseDown(canvas, At(canvas, to.Attributes.InputGrip));
                Check(!held.Held && to.SourceCount == 1 && to.Sources[0] == from,
                    "Clicking another component's input joins the held wire to it");
                Check(held.RespondToMouseUp(canvas, At(canvas, to.Attributes.InputGrip)) == GH_ObjectResponse.Release,
                    "The release that follows that press does nothing a second time");
                to.RemoveAllSources();

                // The same click made inside the capsule is Grasshopper's: that release
                // is what selects the component, and holding a wire there is what once
                // left a dead ring around every input and output.
                var inside = Start(canvas, from, false, false);
                answer = inside.RespondToMouseUp(canvas, At(canvas, from.Attributes.OutputGrip));
                Check(answer != GH_ObjectResponse.Ignore && !inside.Held,
                    "A click inside the capsule is handed back to Grasshopper, which selects");

                // A held wire put down on bare canvas asks for a favorite instead.
                var asking = Start(canvas, from, false, true);
                asking.RespondToMouseUp(canvas, At(canvas, from.Attributes.OutputGrip));
                Check(asking.Held, "The wire is riding the cursor before it is put down");
                answer = asking.RespondToMouseDown(canvas, At(canvas, far));
                Check(answer == GH_ObjectResponse.Release && !asking.Held && to.SourceCount == 0,
                    "Putting a held wire down on bare canvas asks for a favorite, and joins nothing");

                // A group is bare canvas as far as this gesture is concerned.
                var group = new GH_Group();
                Add(doc, group, 0, 0); group.AddObject(to.InstanceGuid);
                group.Attributes.ExpireLayout(); group.Attributes.PerformLayout();
                var corner = new PointF(group.Attributes.Bounds.Left + 3, group.Attributes.Bounds.Top + 3);
                var onGroup = Start(canvas, from, false, true);
                onGroup.RespondToMouseUp(canvas, At(canvas, from.Attributes.OutputGrip));
                answer = onGroup.RespondToMouseDown(canvas, At(canvas, corner));
                Check(answer == GH_ObjectResponse.Release && to.SourceCount == 0,
                    "A group counts as canvas: the favorites are asked for, not a connection");

                // Dragging still works exactly as it did, with no click in the middle.
                var dragged = Start(canvas, from, false, true);
                dragged.RespondToMouseMove(canvas, At(canvas, new PointF(300, 160)));
                answer = dragged.RespondToMouseUp(canvas, At(canvas, to.Attributes.InputGrip));
                Check(!dragged.Held && to.SourceCount == 1,
                    "A held drag released on an input joins it, without ever riding the cursor");
                to.RemoveAllSources();

                canvas.Document = null;
            }
            log.WriteLine(count + " wire gesture checks passed.");
        }
    }
}
