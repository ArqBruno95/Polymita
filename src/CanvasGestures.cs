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
        public static PointF Snap(RectangleF moving, IEnumerable<RectangleF> targets, float tolerance,
            out float? guideX, out float? guideY)
        {
            guideX = guideY = null;
            float dx = 0, dy = 0, bestX = tolerance, bestY = tolerance;
            foreach (var target in targets)
            {
                foreach (float x in new[] { moving.Left, moving.Left + moving.Width / 2, moving.Right })
                    foreach (float tx in new[] { target.Left, target.Left + target.Width / 2, target.Right })
                        if (Math.Abs(tx - x) < bestX) { bestX = Math.Abs(tx - x); dx = tx - x; guideX = tx; }
                foreach (float y in new[] { moving.Top, moving.Top + moving.Height / 2, moving.Bottom })
                    foreach (float ty in new[] { target.Top, target.Top + target.Height / 2, target.Bottom })
                        if (Math.Abs(ty - y) < bestY) { bestY = Math.Abs(ty - y); dy = ty - y; guideY = ty; }
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
        PointF previous;
        bool finished;
        internal CutInteraction(GH_Canvas canvas, GH_CanvasMouseEvent e) : base(canvas,e)
        { m_active=true; doc=canvas.Document; previous=e.CanvasLocation; canvas.Capture=true; canvas.Cursor=Cursors.Cross; }
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (sender.Document != doc) return GH_ObjectResponse.Release;
            if ((Control.ModifierKeys & Keys.Control) == 0) { Commit(); return GH_ObjectResponse.Release; }
            Sweep(e.CanvasLocation); sender.Invalidate(); return GH_ObjectResponse.Handled;
        }
        internal void Sweep(PointF point)
        {
            float tolerance=3F/Math.Max(0.05F,Canvas.Viewport.Zoom);
            foreach (var input in doc.Objects.SelectMany(o=>Recipes.Ports(o,false)).ToArray())
            {
                if (input.WireDisplay == GH_ParamWireDisplay.hidden || !input.Attributes.HasInputGrip) continue;
                foreach (var source in input.Sources.ToArray())
                {
                    if (source.Attributes == null || !source.Attributes.HasOutputGrip) continue;
                    using (var path=GH_Painter.ConnectionPath(source.Attributes.OutputGrip,input.Attributes.InputGrip,GH_WireDirection.right,GH_WireDirection.left))
                    {
                        path.Flatten(null,0.5F/Math.Max(0.05F,Canvas.Viewport.Zoom));
                        var pts=path.PathPoints; bool hit=false;
                        for (int i=1;i<pts.Length;i++)
                            if (AlignmentGeometry.Crosses(previous,point,pts[i-1],pts[i],tolerance)) { hit=true; break; }
                        if (!hit) continue;
                        if (!originals.ContainsKey(input)) { originals.Add(input,input.Sources.ToArray()); undo.AddAction(new GH_WireAction(input)); }
                        input.RemoveSource(source);
                    }
                }
            }
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
            if (!finished) foreach (var pair in originals) { pair.Key.RemoveAllSources(); foreach(var source in pair.Value) pair.Key.AddSource(source); }
            Canvas.Cursor=Cursors.Default; Canvas.Capture=false; Canvas.Invalidate(); base.Destroy();
        }
    }

    internal sealed class AlignInteraction : GH_AbstractInteraction
    {
        readonly GH_Document doc;
        readonly List<IGH_DocumentObject> objects;
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
            undo=doc.UndoUtil.CreatePivotEvent("Polymita: align selection",objects);
            canvas.CanvasPostPaintObjects+=Paint; canvas.Capture=true;
        }
        void Position(PointF delta)
        {
            foreach(var obj in objects.Where(o=>!(o is GH_Group) || ((GH_Group)o).ObjectIDs.Count==0))
            { var p=pivots[obj]; obj.Attributes.Pivot=new PointF(p.X+delta.X,p.Y+delta.Y); obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout(); }
            foreach(var g in doc.Objects.OfType<GH_Group>()) { g.Attributes.ExpireLayout(); g.Attributes.PerformLayout(); }
        }
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender,GH_CanvasMouseEvent e)
        {
            if(sender.Document!=doc) return GH_ObjectResponse.Release;
            var delta=new PointF(e.CanvasX-CanvasPointDown.X,e.CanvasY-CanvasPointDown.Y);
            if(!moved && Math.Abs(delta.X)*sender.Viewport.Zoom<3 && Math.Abs(delta.Y)*sender.Viewport.Zoom<3) return GH_ObjectResponse.Handled;
            var box=bounds; box.Offset(delta);
            var snap=AlignmentGeometry.Snap(box,targets,8F/Math.Max(0.05F,sender.Viewport.Zoom),out guideX,out guideY);
            delta.X+=snap.X; delta.Y+=snap.Y; Position(delta); moved=delta.X!=0 || delta.Y!=0;
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
