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
        private static void Consider(float value, float reference, ref float best, ref float delta, ref float? guide)
        {
            var gap = reference - value; if (gap < 0) gap = -gap;
            // Negated rather than >=, so a NaN bound is skipped exactly as Math.Abs was.
            if (!(gap < best)) return;
            best = gap; delta = reference - value; guide = reference;
        }
        // Runs for every target on every mouse move of a drag. The candidate edges are
        // hoisted out of the loop: building the two three-element arrays per target was
        // the whole allocation cost of aligning inside a large definition.
        public static PointF Snap(RectangleF moving, IEnumerable<RectangleF> targets, float tolerance,
            out float? guideX, out float? guideY)
        {
            guideX = guideY = null;
            float dx = 0, dy = 0, bestX = tolerance, bestY = tolerance;
            float x0 = moving.Left, x1 = moving.Left + moving.Width / 2, x2 = moving.Right;
            float y0 = moving.Top, y1 = moving.Top + moving.Height / 2, y2 = moving.Bottom;
            foreach (var target in targets)
            {
                float tx0 = target.Left, tx1 = target.Left + target.Width / 2, tx2 = target.Right;
                Consider(x0, tx0, ref bestX, ref dx, ref guideX); Consider(x0, tx1, ref bestX, ref dx, ref guideX); Consider(x0, tx2, ref bestX, ref dx, ref guideX);
                Consider(x1, tx0, ref bestX, ref dx, ref guideX); Consider(x1, tx1, ref bestX, ref dx, ref guideX); Consider(x1, tx2, ref bestX, ref dx, ref guideX);
                Consider(x2, tx0, ref bestX, ref dx, ref guideX); Consider(x2, tx1, ref bestX, ref dx, ref guideX); Consider(x2, tx2, ref bestX, ref dx, ref guideX);
                float ty0 = target.Top, ty1 = target.Top + target.Height / 2, ty2 = target.Bottom;
                Consider(y0, ty0, ref bestY, ref dy, ref guideY); Consider(y0, ty1, ref bestY, ref dy, ref guideY); Consider(y0, ty2, ref bestY, ref dy, ref guideY);
                Consider(y1, ty0, ref bestY, ref dy, ref guideY); Consider(y1, ty1, ref bestY, ref dy, ref guideY); Consider(y1, ty2, ref bestY, ref dy, ref guideY);
                Consider(y2, ty0, ref bestY, ref dy, ref guideY); Consider(y2, ty1, ref bestY, ref dy, ref guideY); Consider(y2, ty2, ref bestY, ref dy, ref guideY);
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
    internal sealed class CanvasGestures : IMessageFilter, IDisposable
    {
        readonly GH_Canvas canvas;
        internal static bool CutEnabled = true, SnapEnabled = true;
        internal CanvasGestures(GH_Canvas canvas)
        { this.canvas=canvas; Application.AddMessageFilter(this); canvas.MouseDown += Down; }
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != 0x201 || m.HWnd != canvas.Handle || !CutEnabled ||
                (m.WParam.ToInt64() & 0xC) != 0x8 || (Control.ModifierKeys & Keys.Alt) != 0 ||
                canvas.Document == null || canvas.ActiveInteraction != null) return false;
            // Read the coordinates/modifiers of this message, not a later cursor position.
            long location=m.LParam.ToInt64();
            var p = new Point(unchecked((short)(location & 0xffff)),unchecked((short)((location >> 16) & 0xffff)));
            var e = new GH_CanvasMouseEvent(canvas.Viewport, new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));
            var hit = canvas.Document.FindAttribute(e.CanvasLocation,true);
            if (hit != null && !(hit.DocObject is GH_Group)) return false;
            canvas.Focus();
            canvas.ActiveInteraction = new CutInteraction(canvas,e);
            return true;
        }
        void Down(object sender, MouseEventArgs e)
        {
            if (!SnapEnabled || e.Button != MouseButtons.Left || Control.ModifierKeys != Keys.None || canvas.Document == null) return;
            if (canvas.ActiveInteraction == null || canvas.ActiveInteraction.GetType() != typeof(GH_DragInteraction)) return;
            var ev = new GH_CanvasMouseEvent(canvas.Viewport,e);
            var ids = CanvasOperations.ExpandSelection(canvas.Document);
            if (ids.Count == 0) return;
            if (canvas.Document.Objects.Any(o=>ids.Contains(o.InstanceGuid) && !(o is IGH_Component || o is IGH_Param || o is GH_Group))) return;
            canvas.ActiveInteraction = new AlignInteraction(canvas,ev,ids);
        }
        public void Dispose()
        {
            if (canvas.ActiveInteraction is CutInteraction || canvas.ActiveInteraction is AlignInteraction) canvas.ActiveInteraction=null;
            Application.RemoveMessageFilter(this); canvas.MouseDown -= Down;
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
            if ((Control.ModifierKeys & Keys.Control) == 0) { Commit(); return GH_ObjectResponse.Release; }
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
        {
            if(e.KeyCode==Keys.ControlKey || e.KeyCode==Keys.LControlKey || e.KeyCode==Keys.RControlKey)
            { if(sender.Document==doc) Commit(); return GH_ObjectResponse.Release; }
            return GH_ObjectResponse.Handled;
        }
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
        readonly GH_UndoRecord undo;
        float? guideX, guideY;
        bool committed, moved;
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
            // Alt suspends alignment for the rest of the stroke. Holding any modifier
            // before pressing already keeps the native drag; this makes the same
            // modifier work once the drag is under way.
            if((Control.ModifierKeys & Keys.Alt)!=0) guideX=guideY=null;
            else
            {
                var box=bounds; box.Offset(delta);
                var snap=AlignmentGeometry.Snap(box,targets,8F/Math.Max(0.05F,sender.Viewport.Zoom),out guideX,out guideY);
                delta.X+=snap.X; delta.Y+=snap.Y;
            }
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
        void Paint(GH_Canvas sender)
        {
            using(var pen=new Pen(Ui.Accent,1F/Math.Max(0.05F,sender.Viewport.Zoom)))
            {
                pen.DashStyle=DashStyle.Dash;
                var r=sender.Viewport.VisibleRegion;
                if(guideX.HasValue) sender.Graphics.DrawLine(pen,guideX.Value,r.Top,guideX.Value,r.Bottom);
                if(guideY.HasValue) sender.Graphics.DrawLine(pen,r.Left,guideY.Value,r.Right,guideY.Value);
            }
        }
        public override void Destroy()
        {
            if(!committed) Position(PointF.Empty);
            Canvas.CanvasPostPaintObjects-=Paint; Canvas.Capture=false; Canvas.Invalidate(); base.Destroy();
        }
    }
}
