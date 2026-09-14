using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using WireShelf;

public static class GestureTests
{
    static TextWriter log; static int count;
    static void Check(bool yes,string name) { if(!yes)throw new Exception(name); log.WriteLine("PASS "+name); log.Flush(); count++; }
    static T Add<T>(GH_Document doc,T obj,float x,float y) where T:IGH_DocumentObject
    { obj.CreateAttributes();obj.Attributes.Pivot=new PointF(x,y);doc.AddObject(obj,false);obj.Attributes.ExpireLayout();obj.Attributes.PerformLayout();return obj; }
    static GH_CanvasMouseEvent E(float x,float y) { return new GH_CanvasMouseEvent(Point.Empty,new PointF(x,y),MouseButtons.Left,1,0); }
    public static void Run(string root)
    {
        var canvas=Instances.ActiveCanvas; var original=canvas.Document;
        count=0;
        using(log=new StreamWriter(Path.Combine(root,"test-output","gestures.txt")))
        try {
            float? x,y;
            var delta=AlignmentGeometry.Snap(new RectangleF(100,96,40,40),new[]{new RectangleF(300,100,40,40)},8,out x,out y);
            Check(delta.Y==4 && !x.HasValue,"Edges align within screen tolerance");
            delta=AlignmentGeometry.Snap(new RectangleF(100,109,10,10),new[]{new RectangleF(300,100,40,40)},8,out x,out y);
            Check(delta.Y==1 && y==120,"Center and edges use nearest reference");
            delta=AlignmentGeometry.Snap(new RectangleF(100,160,40,40),new[]{new RectangleF(300,100,40,40)},8,out x,out y);
            Check(delta==PointF.Empty,"Outside tolerance remains unsnapped");
            Check(AlignmentGeometry.Crosses(new PointF(0,0),new PointF(100,100),new PointF(0,100),new PointF(100,0),0),"Fast sweep crosses between mouse samples");
            Check(!AlignmentGeometry.Crosses(new PointF(0,0),new PointF(10,0),new PointF(20,0),new PointF(30,0),2),"Separated collinear strokes do not cut");
            Check(AlignmentGeometry.Crosses(new PointF(5,1),new PointF(5,1),new PointF(0,0),new PointF(10,0),2),"Stationary hit respects tolerance");
            using(var doc=new GH_Document()) {
                canvas.Document=doc;
                var a=Add(doc,new Param_Number(),100,100);
                var b=Add(doc,new Param_Number(),100,200);
                var target=Add(doc,new Param_Number(),500,150); target.AddSource(a);target.AddSource(b);
                using(var filter=new CanvasGestures(canvas)) {
                    var pt=canvas.Viewport.ProjectPoint(new PointF(300,0));
                    var packed=new IntPtr(((int)pt.Y<<16) | ((int)pt.X & 0xffff));
                    var message=Message.Create(canvas.Handle,0x201,new IntPtr(9),packed);
                    Check(filter.PreFilterMessage(ref message),"Ctrl-left canvas message starts cutter");
                    Check(canvas.ActiveInteraction is CutInteraction && canvas.ActiveInteraction.IsActive,"Cutter owns an active canvas interaction");
                    canvas.ActiveInteraction=null;
                    message=Message.Create(canvas.Handle,0x201,new IntPtr(1),packed);
                    Check(!filter.PreFilterMessage(ref message),"Ordinary clicks remain native");
                }
                var cut=new CutInteraction(canvas,E(300,0));
                cut.Sweep(new PointF(300,300));
                Check(target.SourceCount==0,"One sweep cuts multiple wires");
                Check(cut.Stroke.Count==2 && cut.Stroke[0]==new PointF(300,0) && cut.Stroke[1]==new PointF(300,300),"Sweep records the stroke drawn on the canvas");
                cut.Sweep(new PointF(300,300));
                Check(cut.Stroke.Count==2,"A repeated sample does not grow the stroke");
                cut.RespondToMouseUp(canvas,E(300,300));cut.Destroy();
                doc.Undo();Check(target.Sources.SequenceEqual(new[]{a,b}),"Undo restores wires and source order");
                doc.Redo();Check(target.SourceCount==0,"Redo removes both wires");
                doc.Undo();
                cut=new CutInteraction(canvas,E(300,0));cut.Sweep(new PointF(300,300));cut.Destroy();
                Check(target.Sources.SequenceEqual(new[]{a,b}),"Cancelled cut restores source order");
                Check(cut.Stroke.Count==0,"Ending a stroke clears its feedback");
                foreach(var variant in new[]{0,1}) {
                    WireStyles.Variant=variant;WireStyles.SetPolylines(true);
                    cut=new CutInteraction(canvas,E(300,0));cut.Sweep(new PointF(300,300));
                    Check(target.SourceCount==0,"Cuts displayed polyline variant "+variant);cut.Destroy();
                }
                WireStyles.SetPolylines(false);
                target.WireDisplay=GH_ParamWireDisplay.hidden;
                cut=new CutInteraction(canvas,E(300,0));cut.Sweep(new PointF(300,300));cut.Destroy();
                Check(target.SourceCount==2,"Hidden wires are preserved");
                target.WireDisplay=GH_ParamWireDisplay.@default;
                var inner=Add(doc,new GH_Group(),0,0);inner.AddObject(a.InstanceGuid);inner.AddObject(b.InstanceGuid);
                var outer=Add(doc,new GH_Group(),0,0);outer.AddObject(inner.InstanceGuid);
                foreach(var obj in doc.Objects) { obj.Attributes.ExpireLayout();obj.Attributes.PerformLayout(); }
                doc.DeselectAll();outer.Attributes.Selected=true;
                var ap=a.Attributes.Pivot;var bp=b.Attributes.Pivot;var tp=target.Attributes.Pivot;
                var move=new AlignInteraction(canvas,E(0,0),CanvasOperations.ExpandSelection(doc));
                move.RespondToMouseMove(canvas,E(70,55));move.RespondToMouseUp(canvas,E(70,55));move.Destroy();
                Check(a.Attributes.Pivot!=ap && b.Attributes.Pivot.X-a.Attributes.Pivot.X==bp.X-ap.X && b.Attributes.Pivot.Y-a.Attributes.Pivot.Y==bp.Y-ap.Y,"Nested group translates without changing internal layout");
                Check(target.Attributes.Pivot==tp,"Unselected objects do not move");
                var after=a.Attributes.Pivot;
                doc.Undo();Check(a.Attributes.Pivot==ap && b.Attributes.Pivot==bp,"Group move has single-step Undo");
                doc.Redo();Check(a.Attributes.Pivot==after,"Group move Redo restores placement");
                move=new AlignInteraction(canvas,E(0,0),CanvasOperations.ExpandSelection(doc));
                move.RespondToMouseMove(canvas,E(70,55));move.Destroy();
                Check(a.Attributes.Pivot==after,"Cancelled movement restores position");
                // A right-to-left wire leaves its path outside the box of its own grips,
                // which is the case the sweep's rejection box has to stay generous about.
                var feed=Add(doc,new Param_Number(),600,900);
                var back=Add(doc,new Param_Number(),100,900); back.AddSource(feed);
                foreach(var obj in doc.Objects) { obj.Attributes.ExpireLayout();obj.Attributes.PerformLayout(); }
                cut=new CutInteraction(canvas,E(300,800));cut.Sweep(new PointF(300,1000));
                Check(back.SourceCount==0,"Backwards wire is reached outside the box of its grips");
                cut.RespondToMouseUp(canvas,E(300,1000));cut.Destroy();
                cut=new CutInteraction(canvas,E(300,800));cut.Sweep(new PointF(300,1000));
                Check(target.SourceCount==2,"A stroke far from a wire leaves it connected");
                cut.Destroy();
            }
            log.WriteLine(count+" gesture checks passed.");
        }
        finally { canvas.Document=original;canvas.Invalidate(); }
    }
}
