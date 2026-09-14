using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Undo;
using Grasshopper.Kernel.Undo.Actions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace WireShelf
{
    public static class AlignmentGeometry
    {
        // What produced a guide, which is also how it is coloured on the canvas.
        public const int None = 0, Edge = 1, Centre = 2, Port = 3;
        // How much further than the plain tolerance a port may pull the moving centre
        // along the horizontal axis.
        public const float PortReach = 4F;
        // Candidates are scored as a fraction of their own reach, so a reference with a
        // wider window competes fairly rather than simply overruling a closer one.
        private static void Consider(float value, float reference, int kind, float reach,
            ref float best, ref float delta, ref float? guide, ref int bestKind)
        {
            var gap = reference - value; if (gap < 0) gap = -gap;
            var score = gap / reach;
            // Negated rather than >=, so a NaN bound is skipped exactly as Math.Abs was.
            if (!(score < best)) return;
            best = score; delta = reference - value; guide = reference; bestKind = kind;
        }
        public static PointF Snap(RectangleF moving, IEnumerable<RectangleF> targets, float tolerance,
            out float? guideX, out float? guideY)
        { int kx, ky; return Snap(moving, targets, null, tolerance, moving.Top + moving.Height/2, out guideX, out guideY, out kx, out ky); }
        // Runs for every target on every mouse move of a drag. The candidate edges are
        // hoisted out of the loop: building the two three-element arrays per target was
        // the whole allocation cost of aligning inside a large definition.
        // anchorY is the height the ports are measured against. A capsule with several
        // outputs has none of them on its centre line, so the drag carries the grip it
        // was grabbed nearest instead, and that is what lines up with the far port.
        public static PointF Snap(RectangleF moving, IEnumerable<RectangleF> targets, IEnumerable<PointF> ports,
            float tolerance, float anchorY, out float? guideX, out float? guideY, out int kindX, out int kindY)
        {
            guideX = guideY = null; kindX = kindY = None;
            float dx = 0, dy = 0, bestX = 1F, bestY = 1F;
            float x0 = moving.Left, x1 = moving.Left + moving.Width / 2, x2 = moving.Right;
            float y0 = moving.Top, y1 = moving.Top + moving.Height / 2, y2 = moving.Bottom;
            foreach (var target in targets)
            {
                // Centre against centre is the one pairing that marks a shared axis;
                // everything else lines an edge up with something.
                float tx0 = target.Left, tx1 = target.Left + target.Width / 2, tx2 = target.Right;
                Consider(x0, tx0, Edge, tolerance, ref bestX, ref dx, ref guideX, ref kindX); Consider(x0, tx1, Edge, tolerance, ref bestX, ref dx, ref guideX, ref kindX); Consider(x0, tx2, Edge, tolerance, ref bestX, ref dx, ref guideX, ref kindX);
                Consider(x1, tx0, Edge, tolerance, ref bestX, ref dx, ref guideX, ref kindX); Consider(x1, tx1, Centre, tolerance, ref bestX, ref dx, ref guideX, ref kindX); Consider(x1, tx2, Edge, tolerance, ref bestX, ref dx, ref guideX, ref kindX);
                Consider(x2, tx0, Edge, tolerance, ref bestX, ref dx, ref guideX, ref kindX); Consider(x2, tx1, Edge, tolerance, ref bestX, ref dx, ref guideX, ref kindX); Consider(x2, tx2, Edge, tolerance, ref bestX, ref dx, ref guideX, ref kindX);
                float ty0 = target.Top, ty1 = target.Top + target.Height / 2, ty2 = target.Bottom;
                Consider(y0, ty0, Edge, tolerance, ref bestY, ref dy, ref guideY, ref kindY); Consider(y0, ty1, Edge, tolerance, ref bestY, ref dy, ref guideY, ref kindY); Consider(y0, ty2, Edge, tolerance, ref bestY, ref dy, ref guideY, ref kindY);
                Consider(y1, ty0, Edge, tolerance, ref bestY, ref dy, ref guideY, ref kindY); Consider(y1, ty1, Centre, tolerance, ref bestY, ref dy, ref guideY, ref kindY); Consider(y1, ty2, Edge, tolerance, ref bestY, ref dy, ref guideY, ref kindY);
                Consider(y2, ty0, Edge, tolerance, ref bestY, ref dy, ref guideY, ref kindY); Consider(y2, ty1, Edge, tolerance, ref bestY, ref dy, ref guideY, ref kindY); Consider(y2, ty2, Edge, tolerance, ref bestY, ref dy, ref guideY, ref kindY);
            }
            // Grips of the components this selection is actually wired to; only the
            // moving centre lines up with them.
            if (ports != null)
                foreach (var port in ports)
                {
                    Consider(x1, port.X, Port, tolerance, ref bestX, ref dx, ref guideX, ref kindX);
                    // Lining the moving centre up with the grip it wires into is the one
                    // the drag is usually reaching for, so it grabs from further away.
                    Consider(anchorY, port.Y, Port, tolerance*PortReach, ref bestY, ref dy, ref guideY, ref kindY);
                }
            return new PointF(dx, dy);
        }
        public static bool Crosses(PointF a, PointF b, PointF c, PointF d, float tolerance)
        {
            float ux = b.X-a.X, uy = b.Y-a.Y, vx = d.X-c.X, vy = d.Y-c.Y;
            float cross = ux*vy-uy*vx;
            if (Math.Abs(cross) > 0.000001F)
            {
                float t = ((c.X-a.X)*vy-(c.Y-a.Y)*vx)/cross;
                float s = ((c.X-a.X)*uy-(c.Y-a.Y)*ux)/cross;
                if (t >= 0 && t <= 1 && s >= 0 && s <= 1) return true;
            }
            return ShelfRuntime.Distance(a, WireStyles.Closest(c,d,a)) <= tolerance ||
                ShelfRuntime.Distance(b, WireStyles.Closest(c,d,b)) <= tolerance ||
                ShelfRuntime.Distance(c, WireStyles.Closest(a,b,c)) <= tolerance ||
                ShelfRuntime.Distance(d, WireStyles.Closest(a,b,d)) <= tolerance;
        }
    }

    // Only intercept the canvas itself; Ctrl-click on an object keeps native multiselection.
    internal sealed class CanvasGestures : IDisposable
    {
        readonly GH_Canvas canvas;
        internal static bool CutEnabled = true, SnapEnabled = true;
        internal CanvasGestures(GH_Canvas canvas)
        { this.canvas=canvas; canvas.MouseDown += Down; }
        void Down(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || canvas.Document == null) return;
            if (StartCut(new GH_CanvasMouseEvent(canvas.Viewport,e), Control.ModifierKeys)) return;
            StartAlign(e);
        }
        // Rhino pumps its own messages, so Application.AddMessageFilter never saw the
        // canvas's ordinary mouse messages — the same reason the double-Shift gesture
        // needs a keyboard hook. It only ran while something else pumped through
        // WinForms, such as the modal loop a right button opens, which is why the
        // gesture used to need a second button to fire. The canvas event always runs.
        // Grasshopper has begun its own interaction by then; replacing it is what the
        // alignment gesture already does, and the selection rectangle Ctrl starts
        // applies nothing until a mouse-up that it will now never receive.
        internal bool StartCut(GH_CanvasMouseEvent e, Keys modifiers)
        {
            if (!CutEnabled || modifiers != Keys.Control || canvas.Document == null) return false;
            if (canvas.ActiveInteraction is CutInteraction) return true;
            var hit = canvas.Document.FindAttribute(e.CanvasLocation,true);
            if (hit != null && !(hit.DocObject is GH_Group)) return false;
            canvas.Focus();
            canvas.ActiveInteraction = new CutInteraction(canvas,e);
            return true;
        }
        void StartAlign(MouseEventArgs e)
        {
            // Ctrl belongs to the cutter and to native multiselection. Shift constrains
            // the move and Alt copies, so both have to reach the drag interaction.
            var modifiers = Control.ModifierKeys & (Keys.Control|Keys.Shift|Keys.Alt);
            if ((modifiers & Keys.Control) != 0) return;
            if (!SnapEnabled && modifiers == Keys.None) return;
            // Grasshopper starts its drag before this event runs, but not for every
            // modifier. Taking over a null interaction as well is safe as long as the
            // press really landed on something already selected, which is a drag.
            var live = canvas.ActiveInteraction;
            if (live != null && live.GetType() != typeof(GH_DragInteraction)) return;
            var ev = new GH_CanvasMouseEvent(canvas.Viewport,e);
            var ids = CanvasOperations.ExpandSelection(canvas.Document);
            if (ids.Count == 0) return;
            var under = canvas.Document.FindAttribute(ev.CanvasLocation, true);
            if (under == null || !ids.Contains(under.GetTopLevel.DocObject.InstanceGuid)) return;
            if (canvas.Document.Objects.Any(o=>ids.Contains(o.InstanceGuid) && !(o is IGH_Component || o is IGH_Param || o is GH_Group))) return;
            canvas.ActiveInteraction = new AlignInteraction(canvas,ev,ids);
        }
        public void Dispose()
        {
            if (canvas.ActiveInteraction is CutInteraction || canvas.ActiveInteraction is AlignInteraction) canvas.ActiveInteraction=null;
            canvas.MouseDown -= Down;
        }
    }

    internal sealed class CutInteraction : GH_AbstractInteraction
    {
        readonly GH_Document doc;
        readonly Dictionary<IGH_Param, IGH_Param[]> originals = new Dictionary<IGH_Param, IGH_Param[]>();
        readonly GH_UndoRecord undo = new GH_UndoRecord("Polymita: cut wires");
        readonly List<PointF> stroke = new List<PointF>();
        readonly IGH_Param[] inputs;
        PointF previous;
        bool finished;
        internal CutInteraction(GH_Canvas canvas, GH_CanvasMouseEvent e) : base(canvas,e)
        {
            m_active=true; doc=canvas.Document; previous=e.CanvasLocation; stroke.Add(previous);
            // The stroke holds the mouse, so no object can join or leave the document
            // while it runs. Collecting the recipients once keeps the per-move work
            // proportional to the wires actually near the stroke.
            inputs=doc.Objects.SelectMany(o=>Recipes.Ports(o,false)).ToArray();
            canvas.CanvasPostPaintObjects+=Paint; canvas.Capture=true; canvas.Cursor=Cursors.Cross;
        }
        internal IList<PointF> Stroke { get { return stroke; } }
        // Cut wires disappear as the stroke advances, so the swept path is the only
        // remaining evidence of what the gesture reached. Draw it in document
        // coordinates and scale the pen so it stays two screen pixels at any zoom.
        void Paint(GH_Canvas sender)
        {
            if (stroke.Count < 2) return;
            using (var pen = new Pen(Ui.Accent, 2F/Math.Max(0.05F,sender.Viewport.Zoom)))
            {
                pen.DashStyle=DashStyle.Dash; pen.StartCap=LineCap.Round; pen.EndCap=LineCap.Round;
                sender.Graphics.DrawLines(pen, stroke.ToArray());
            }
        }
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (sender.Document != doc) return GH_ObjectResponse.Release;
            // The stroke lasts while the left button is held. Letting go of Ctrl part-way
            // no longer ends it, so a long sweep does not depend on holding the modifier,
            // and a mouse-up that never arrives cannot leave the gesture running.
            if ((Control.MouseButtons & MouseButtons.Left) == 0) { Commit(); return GH_ObjectResponse.Release; }
            Sweep(e.CanvasLocation); sender.Invalidate(); return GH_ObjectResponse.Handled;
        }
        internal void Sweep(PointF point)
        {
            float tolerance=3F/Math.Max(0.05F,Canvas.Viewport.Zoom);
            float minX=Math.Min(previous.X,point.X), maxX=Math.Max(previous.X,point.X);
            float minY=Math.Min(previous.Y,point.Y), maxY=Math.Max(previous.Y,point.Y);
            foreach (var input in inputs)
            {
                if (input.WireDisplay == GH_ParamWireDisplay.hidden || input.Attributes == null || !input.Attributes.HasInputGrip) continue;
                var to=input.Attributes.InputGrip;
                foreach (var source in input.Sources.ToArray())
                {
                    if (source.Attributes == null || !source.Attributes.HasOutputGrip) continue;
                    var from=source.Attributes.OutputGrip;
                    // Both the native curve and the polyline variants stay inside the convex
                    // hull of their control points, whose Y values are exactly the two grips'.
                    // Horizontally the hull can reach one span past each grip. Rejecting on
                    // that box first avoids building and flattening far-away wire paths.
                    if (Math.Max(from.Y,to.Y)+tolerance<minY || Math.Min(from.Y,to.Y)-tolerance>maxY) continue;
                    var margin=Math.Max(64F,Math.Abs(to.X-from.X))+tolerance;
                    if (Math.Max(from.X,to.X)+margin<minX || Math.Min(from.X,to.X)-margin>maxX) continue;
                    using (var path=GH_Painter.ConnectionPath(from,to,GH_WireDirection.right,GH_WireDirection.left))
                    {
                        // A degenerate path exposes no PathPoints; reading them would
                        // throw out of the mouse loop and into Grasshopper's pump.
                        if (path==null || path.PointCount==0) continue;
                        path.Flatten(null,0.5F/Math.Max(0.05F,Canvas.Viewport.Zoom));
                        if (path.PointCount<2) continue;
                        var pts=path.PathPoints; bool hit=false;
                        for (int i=1;i<pts.Length;i++)
                            if (AlignmentGeometry.Crosses(previous,point,pts[i-1],pts[i],tolerance)) { hit=true; break; }
                        if (!hit) continue;
                        if (!originals.ContainsKey(input)) { originals.Add(input,input.Sources.ToArray()); undo.AddAction(new GH_WireAction(input)); }
                        input.RemoveSource(source);
                    }
                }
            }
            if (point!=previous) stroke.Add(point);
            previous=point;
        }
        void Commit()
        {
            if (finished) return; finished=true;
            if (originals.Count == 0) return;
            doc.UndoUtil.RecordEvent(undo);
            foreach (var input in originals.Keys) input.ExpireSolution(false);
            doc.NewSolution(false);
        }
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        { if (sender.Document == doc && e.Button == MouseButtons.Left) { Sweep(e.CanvasLocation); Commit(); } return GH_ObjectResponse.Release; }
        public override GH_ObjectResponse RespondToKeyDown(GH_Canvas sender, KeyEventArgs e)
        { return e.KeyCode == Keys.Escape ? GH_ObjectResponse.Release : GH_ObjectResponse.Handled; }
        public override GH_ObjectResponse RespondToKeyUp(GH_Canvas sender, KeyEventArgs e)
        { return GH_ObjectResponse.Handled; }
        public override void Destroy()
        {
            if (!finished && originals.Count>0)
            {
                foreach (var pair in originals) { pair.Key.RemoveAllSources(); foreach(var source in pair.Value) pair.Key.AddSource(source); }
                // Restoring sources expires the recipients. Recompute so a cancelled
                // stroke does not leave the definition in an expired state.
                foreach (var input in originals.Keys) input.ExpireSolution(false);
                doc.NewSolution(false);
            }
            stroke.Clear(); Canvas.CanvasPostPaintObjects-=Paint;
            Canvas.Cursor=Cursors.Default; Canvas.Capture=false; Canvas.Invalidate(); base.Destroy();
        }
    }

    internal sealed class AlignInteraction : GH_AbstractInteraction
    {
        readonly GH_Document doc;
        readonly List<IGH_DocumentObject> objects;
        readonly IGH_DocumentObject[] movers;
        readonly GH_Group[] affected;
        readonly Dictionary<IGH_DocumentObject,PointF> pivots;
        readonly RectangleF bounds;
        readonly RectangleF[] targets;
        readonly PointF[] ports;
        float anchorY;
        readonly GH_UndoRecord undo;
        float? guideX, guideY;
        int kindX, kindY;
        bool committed, moved, copied;
        // Alt is read while the drag runs rather than when it starts: Grasshopper does
        // not always begin a drag under a held modifier, and reading it here also lets
        // the key be pressed part-way through, the way a copy-drag usually works.
        // The originals go back to where they started, a copy is left there, and the
        // originals carry on under the cursor.
        void LeaveCopy()
        {
            if (copied || (Control.ModifierKeys & Keys.Alt) == 0) return;
            copied = true;
            Position(PointF.Empty);
            if (!Ui.Try(delegate { CanvasOperations.DuplicateInPlace(doc); })) return;
            doc.DeselectAll();
            foreach (var obj in objects) obj.Attributes.Selected = true;
        }
        static void Nearest(PointF grip, PointF cursor, ref float best, ref float anchorY)
        {
            var gap=ShelfRuntime.Distance(grip,cursor);
            if(gap>=best) return;
            best=gap; anchorY=grip.Y;
        }
        static void Grip(List<PointF> into, IGH_Param param, HashSet<Guid> moving, bool output)
        {
            if (param == null || param.Attributes == null) return;
            if (moving.Contains(param.Attributes.GetTopLevel.DocObject.InstanceGuid)) return;
            into.Add(output ? param.Attributes.OutputGrip : param.Attributes.InputGrip);
        }
        internal AlignInteraction(GH_Canvas canvas,GH_CanvasMouseEvent e,HashSet<Guid> ids) : base(canvas,e)
        {
            m_active=true; doc=canvas.Document;
            objects=doc.Objects.Where(o=>ids.Contains(o.InstanceGuid)).ToList();
            pivots=objects.ToDictionary(o=>o,o=>o.Attributes.Pivot);
            bounds=objects.Select(o=>o.Attributes.Bounds).Aggregate(RectangleF.Union);
            // Parent groups containing moving objects cannot be stationary references.
            var excluded=new HashSet<Guid>(ids);
            bool changed;
            do { changed=false; foreach(var g in doc.Objects.OfType<GH_Group>())
                if(g.ObjectIDs.Any(excluded.Contains) && excluded.Add(g.InstanceGuid)) changed=true; } while(changed);
            targets=doc.Objects.Where(o=>!excluded.Contains(o.InstanceGuid) && (o is IGH_Component || o is IGH_Param || o is GH_Group))
                .Select(o=>o.Attributes.Bounds).ToArray();
            // Only a group holding something that moves changes shape, and a parent has to
            // read updated children, so they are laid out innermost first. Re-laying out
            // every group in the document on every mouse move was the cost here.
            var sizes=new Dictionary<Guid,int>();
            affected=doc.Objects.OfType<GH_Group>().Where(g=>excluded.Contains(g.InstanceGuid))
                .OrderBy(g=>Contained(doc,g.InstanceGuid,sizes)).ToArray();
            movers=objects.Where(o=>!(o is GH_Group) || ((GH_Group)o).ObjectIDs.Count==0).ToArray();
            // Grips of everything this selection is wired to, so the moving centre can
            // line up with the port it connects to rather than only with bounding boxes.
            var grips=new List<PointF>();
            foreach(var obj in objects)
                foreach(var side in new[]{false,true})
                    foreach(var port in Recipes.Ports(obj,side))
                    {
                        foreach(var source in port.Sources) Grip(grips,source,ids,true);
                        foreach(var recipient in port.Recipients) Grip(grips,recipient,ids,false);
                    }
            ports=grips.ToArray();
            // The moving grip closest to where the drag began. With one output that is
            // effectively the centre line; with several it is the one being reached for.
            anchorY=bounds.Top+bounds.Height/2;
            var closest=float.MaxValue;
            foreach(var obj in objects)
                foreach(var side in new[]{false,true})
                    foreach(var port in Recipes.Ports(obj,side))
                    {
                        if(port.Attributes==null) continue;
                        if(side && port.Attributes.HasOutputGrip) Nearest(port.Attributes.OutputGrip,e.CanvasLocation,ref closest,ref anchorY);
                        if(!side && port.Attributes.HasInputGrip) Nearest(port.Attributes.InputGrip,e.CanvasLocation,ref closest,ref anchorY);
                    }
            undo=doc.UndoUtil.CreatePivotEvent("Polymita: align selection",objects);
            canvas.CanvasPostPaintObjects+=Paint; canvas.Capture=true;
        }
        // Transitive member count: a group always holds strictly more than any group
        // inside it, so ordering by it ascending lays inner groups out first.
        static int Contained(GH_Document doc,Guid id,Dictionary<Guid,int> memo)
        {
            int value;
            if(memo.TryGetValue(id,out value)) return value;
            memo[id]=0; // also breaks a malformed membership cycle
            var group=doc.FindObject(id,false) as GH_Group;
            if(group==null) return 0;
            value=group.ObjectIDs.Count;
            foreach(var child in group.ObjectIDs) value+=Contained(doc,child,memo);
            memo[id]=value; return value;
        }
        void Position(PointF delta)
        {
            foreach(var obj in movers)
            { var p=pivots[obj]; obj.Attributes.Pivot=new PointF(p.X+delta.X,p.Y+delta.Y); obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout(); }
            foreach(var g in affected) { g.Attributes.ExpireLayout(); g.Attributes.PerformLayout(); }
        }
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender,GH_CanvasMouseEvent e)
        {
            if(sender.Document!=doc) return GH_ObjectResponse.Release;
            var delta=new PointF(e.CanvasX-CanvasPointDown.X,e.CanvasY-CanvasPointDown.Y);
            if(!moved && Math.Abs(delta.X)*sender.Viewport.Zoom<3 && Math.Abs(delta.Y)*sender.Viewport.Zoom<3) return GH_ObjectResponse.Handled;
            LeaveCopy();
            // Shift picks the axis from the raw drag and holds the other one still,
            // before and after snapping, so alignment cannot reintroduce the movement
            // the modifier just took away.
            var locked=(Control.ModifierKeys & Keys.Shift)==0 ? 0 : Math.Abs(delta.X)>=Math.Abs(delta.Y) ? 1 : 2;
            if(locked==1) delta.Y=0; else if(locked==2) delta.X=0;
            if(CanvasGestures.SnapEnabled)
            {
                var box=bounds; box.Offset(delta);
                var snap=AlignmentGeometry.Snap(box,targets,ports,8F/Math.Max(0.05F,sender.Viewport.Zoom),
                    anchorY+delta.Y,out guideX,out guideY,out kindX,out kindY);
                delta.X+=snap.X; delta.Y+=snap.Y;
            }
            else { guideX=guideY=null; }
            if(locked==1) { delta.Y=0; guideY=null; } else if(locked==2) { delta.X=0; guideX=null; }
            Position(delta); moved=delta.X!=0 || delta.Y!=0;
            sender.Invalidate(); return GH_ObjectResponse.Handled;
        }
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender,GH_CanvasMouseEvent e)
        {
            if(sender.Document==doc && e.Button==MouseButtons.Left) { if(moved) doc.UndoUtil.RecordEvent(undo); committed=true; }
            return GH_ObjectResponse.Release;
        }
        public override GH_ObjectResponse RespondToKeyDown(GH_Canvas sender,KeyEventArgs e)
        { return e.KeyCode==Keys.Escape ? GH_ObjectResponse.Release : GH_ObjectResponse.Handled; }
        // Green lines up an edge, red a centre axis, blue the moving centre with the
        // grip of a component this selection is wired to.
        static Color Ink(int kind)
        {
            if(kind==AlignmentGeometry.Centre) return Color.FromArgb(214,45,45);
            if(kind==AlignmentGeometry.Port) return Color.FromArgb(38,94,222);
            return Color.FromArgb(28,158,72);
        }
        void Paint(GH_Canvas sender)
        {
            var width=1F/Math.Max(0.05F,sender.Viewport.Zoom);
            var r=sender.Viewport.VisibleRegion;
            if(guideX.HasValue)
                using(var pen=new Pen(Ink(kindX),width)) { pen.DashStyle=DashStyle.Dash; sender.Graphics.DrawLine(pen,guideX.Value,r.Top,guideX.Value,r.Bottom); }
            if(guideY.HasValue)
                using(var pen=new Pen(Ink(kindY),width)) { pen.DashStyle=DashStyle.Dash; sender.Graphics.DrawLine(pen,r.Left,guideY.Value,r.Right,guideY.Value); }
        }
        public override void Destroy()
        {
            if(!committed) Position(PointF.Empty);
            Canvas.CanvasPostPaintObjects-=Paint; Canvas.Capture=false; Canvas.Invalidate(); base.Destroy();
        }
    }
}
