using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Polymita;

public static class Regression085Tests
{
    static TextWriter log; static int count;
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); log.WriteLine("PASS "+name); log.Flush(); count++; }
    static T Add<T>(GH_Document doc,T obj,float x,float y) where T:IGH_DocumentObject
    { obj.CreateAttributes(); obj.Attributes.Pivot=new PointF(x,y); doc.AddObject(obj,false); obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout(); return obj; }
    static GH_CanvasMouseEvent E(float x,float y) { return new GH_CanvasMouseEvent(new Point((int)x,(int)y),new PointF(x,y),MouseButtons.Left,1,0); }
    static bool Near(float a,float b) { return Math.Abs(a-b)<0.001F; }
    static int NativeCopyCount(bool group)
    {
        using(var canvas=new GH_Canvas()) using(var doc=new GH_Document())
        {
            canvas.Document=doc;
            var a=Add(doc,new Param_Number(),100,100);var b=Add(doc,new Param_Number(),200,140);b.AddSource(a);
            if(group) { var g=Add(doc,new GH_Group(),0,0);g.AddObject(a.InstanceGuid);g.AddObject(b.InstanceGuid);g.Attributes.PerformLayout();g.Attributes.Selected=true; }
            else a.Attributes.Selected=true;
            int before=doc.Objects.Count;
            var move=new Grasshopper.GUI.Canvas.Interaction.GH_DragInteraction(canvas,E(100,100));
            move.RespondToMouseMove(canvas,E(190,180));move.RespondToKeyDown(canvas,new KeyEventArgs(Keys.Menu));
            move.RespondToKeyUp(canvas,new KeyEventArgs(Keys.Menu));move.RespondToMouseMove(canvas,E(250,200));
            move.RespondToMouseUp(canvas,E(250,200));move.Destroy();
            return doc.Objects.Count-before;
        }
    }
    static DragAnchors Anchors(GH_Document doc,PointF cursor)
    {
        var ids=CanvasOperations.ExpandSelection(doc);
        var selected=doc.Objects.Where(o=>ids.Contains(o.InstanceGuid)).ToList();
        return new DragAnchors(doc,ids,selected,ids,cursor,selected.Select(o=>o.Attributes.Bounds).Aggregate(RectangleF.Union));
    }
    public static void RunGeometry(TextWriter output)
    {
        log=output; count=0; Geometry();
        log.WriteLine(count+" geometry checks passed.");
    }
    static void Geometry()
    {
            Check(typeof(AlignInteraction).BaseType==typeof(Grasshopper.GUI.Canvas.Interaction.GH_DragInteraction),"Alignment extends Grasshopper's native drag implementation");
            foreach(float zoom in new[]{0.5F,1F,2F})
            {
                float t=8F/zoom; float? gx,gy; int kx,ky;
                var box=new RectangleF(100,100,40,40);
                var snap=AlignmentGeometry.Snap(box,new RectangleF[0],new[]{new PointF(120+31/zoom,120+31/zoom)},t,120,out gx,out gy,out kx,out ky);
                Check(Near(snap.X,31/zoom) && Near(snap.Y,31/zoom) && kx==3 && ky==3,"Both port axes capture at 31 screen pixels, zoom "+zoom);
                snap=AlignmentGeometry.Snap(box,new RectangleF[0],new[]{new PointF(120+33/zoom,120+33/zoom)},t,120,out gx,out gy,out kx,out ky);
                Check(snap==PointF.Empty && !gx.HasValue && !gy.HasValue,"Both port axes release beyond 32 screen pixels, zoom "+zoom);
                snap=AlignmentGeometry.Snap(new RectangleF(0,0,0,0),new[]{new RectangleF(9/zoom,9/zoom,0,0)},t,out gx,out gy);
                Check(snap==PointF.Empty,"Edges cannot capture at 9 screen pixels, zoom "+zoom);
                snap=AlignmentGeometry.Snap(new RectangleF(0,0,0,0),new[]{new RectangleF(7/zoom,7/zoom,0,0)},t,out gx,out gy);
                Check(Near(snap.X,7/zoom) && Near(snap.Y,7/zoom),"Edges capture at 7 screen pixels, zoom "+zoom);
                snap=AlignmentGeometry.Snap(box,new[]{box},new[]{new PointF(120+20/zoom,120+20/zoom)},t,120,out gx,out gy,out kx,out ky);
                Check(kx==3 && ky==3,"Ports keep priority even against an exact edge alignment, zoom "+zoom);
            }
    }
    public static void Run(string root)
    {
        RunOnCanvas(root,Instances.ActiveCanvas);
    }
    public static void RunOnCanvas(string root,GH_Canvas canvas)
    {
        var original=canvas.Document; count=0;
        using(log=new StreamWriter(Path.Combine(root,"test-output","regression-085.txt")))
        try
        {
            Geometry();
            using(var doc=new GH_Document())
            {
                canvas.Document=doc;
                var moving=Add(doc,new MultiPortFixture(),300,250);
                var a=Add(doc,new Param_Number(),100,180); var b=Add(doc,new Param_Number(),100,320);
                var c=Add(doc,new Param_Number(),650,180); var d=Add(doc,new Param_Number(),650,320);
                moving.Params.Input[0].AddSource(a); moving.Params.Input[2].AddSource(b);
                c.AddSource(moving.Params.Output[0]); d.AddSource(moving.Params.Output[2]);
                moving.Attributes.Selected=true;
                foreach(bool output in new[]{false,true}) foreach(int index in new[]{0,2})
                {
                    var port=output ? moving.Params.Output[index] : moving.Params.Input[index];
                    var grip=output ? port.Attributes.OutputGrip : port.Attributes.InputGrip;
                    var cursor=new PointF(grip.X+(output?-2:2),grip.Y);
                    var anchor=Anchors(doc,cursor);
                    var remote=output ? (index==0?c:d).Attributes.InputGrip : (index==0?a:b).Attributes.OutputGrip;
                    Check(anchor.LocalPort==port && anchor.IsOutput==output && anchor.Targets.SequenceEqual(new[]{remote}),"Nearest "+(output?"output":"input")+" "+index+" keeps only its own connected endpoint");
                    Check(Near(anchor.AnchorY,grip.Y),"The selected moving grip, not a different port, supplies the alignment height");
                }
                var unused=moving.Params.Input[1].Attributes.InputGrip;
                Check(Anchors(doc,unused).LocalPort!=moving.Params.Input[1],"An unwired input cannot displace the nearest real wire");
                a.Attributes.Selected=true;
                var own=moving.Params.Output[2].Attributes.OutputGrip;
                Check(Anchors(doc,new PointF(own.X-2,own.Y)).LocalPort==moving.Params.Output[2],"Multiple selection uses the grabbed component's wire");
                doc.DeselectAll(); moving.Attributes.Selected=true;
                var before=moving.Attributes.Pivot;
                var grip2=moving.Params.Output[2].Attributes.OutputGrip;
                var down=new PointF(grip2.X-2,grip2.Y);
                float rawY=d.Attributes.InputGrip.Y-grip2.Y-20;
                var move=new AlignInteraction(canvas,E(down.X,down.Y),CanvasOperations.ExpandSelection(doc));
                move.RespondToMouseMove(canvas,E(down.X+65,down.Y+rawY));
                Check(Near(moving.Params.Output[2].Attributes.OutputGrip.Y,d.Attributes.InputGrip.Y),"Dragging straightens the wire selected by the grab point");
                move.Destroy(); Check(moving.Attributes.Pivot==before,"Cancelling the port-aligned move restores its original pivot");
            }
            using(var doc=new GH_Document())
            {
                canvas.Document=doc;
                var source=Add(doc,new Param_Number(),100,100);
                var target=Add(doc,new Param_Number(),500,300);
                source.Attributes.Selected=true;
                var anchor=Anchors(doc,source.Attributes.Pivot);
                Check(anchor.LocalPort==null && anchor.Targets.Contains(target.Attributes.InputGrip) && anchor.Targets.Contains(target.Attributes.OutputGrip),"An unconnected moving object aligns its centres to stationary input and output vertices");
            }
            foreach(bool group in new[]{false,true})
            using(var doc=new GH_Document())
            {
                canvas.Document=doc;
                var a=Add(doc,new Param_Number(),100,100); var b=Add(doc,new Param_Number(),200,140); b.AddSource(a);
                if(group) { var g=Add(doc,new GH_Group(),0,0); g.AddObject(a.InstanceGuid); g.AddObject(b.InstanceGuid); g.Attributes.PerformLayout(); g.Attributes.Selected=true; }
                else a.Attributes.Selected=true;
                var ap=a.Attributes.Pivot; int beforeCount=doc.Objects.Count;
                var move=new AlignInteraction(canvas,E(100,100),CanvasOperations.ExpandSelection(doc));
                move.RespondToMouseMove(canvas,E(190,180));
                Check(a.Attributes.Pivot!=ap,"Plain drag moved before pressing Alt, group="+group);
                move.RespondToKeyDown(canvas,new KeyEventArgs(Keys.Menu));
                Check(a.Attributes.Pivot==ap && doc.Objects.Count==beforeCount,"Alt restores original and defers copying to native release, group="+group);
                move.RespondToKeyDown(canvas,new KeyEventArgs(Keys.Menu));
                move.RespondToKeyUp(canvas,new KeyEventArgs(Keys.Menu));
                move.RespondToMouseMove(canvas,E(250,200));
                move.RespondToMouseUp(canvas,E(250,200)); move.Destroy();
                int copies=NativeCopyCount(group);
                Check(doc.Objects.Count==beforeCount+copies && a.Attributes.Pivot==ap,"Alt release matches unmodified Grasshopper's copy result, group="+group);
                Check(doc.Objects.OfType<Param_Number>().Any(p=>p.Attributes.Pivot==new PointF(ap.X+150,ap.Y+100)),"Native copy uses the original mouse-down offset");
                if(group)
                {
                    var members=doc.Objects.OfType<Param_Number>().Where(p=>p!=a && p!=b).ToArray();
                    Check(members.Length==2 && members.Any(p=>p.Sources.Any(s=>members.Contains(s))),"Native group copy retains the members' internal wire");
                }
                doc.Undo(); Check(doc.Objects.Count==beforeCount && a.Attributes.Pivot==ap,"Single Undo removes native Alt copy, group="+group);
                doc.Redo(); Check(doc.Objects.Count==beforeCount+copies,"Redo restores native Alt copy, group="+group);
            }
            using(var doc=new GH_Document())
            {
                canvas.Document=doc;
                var p=Add(doc,new Param_Number(),100,100); p.Attributes.Selected=true;
                var move=new AlignInteraction(canvas,E(100,100),CanvasOperations.ExpandSelection(doc));
                move.RespondToMouseMove(canvas,E(160,160));
                move.RespondToKeyDown(canvas,new KeyEventArgs(Keys.Menu)); move.RespondToKeyUp(canvas,new KeyEventArgs(Keys.Menu));
                move.RespondToKeyDown(canvas,new KeyEventArgs(Keys.Menu)); move.RespondToKeyUp(canvas,new KeyEventArgs(Keys.Menu));
                move.RespondToMouseMove(canvas,E(200,170)); move.RespondToMouseUp(canvas,E(200,170)); move.Destroy();
                Check(doc.Objects.Count==1 && p.Attributes.Pivot==new PointF(200,170),"A second Alt tap returns to native move without a duplicate");
                doc.Undo(); Check(p.Attributes.Pivot==new PointF(100,100),"Native move after Alt toggling retains single-step Undo");
                move=new AlignInteraction(canvas,E(100,100),CanvasOperations.ExpandSelection(doc));
                move.RespondToMouseMove(canvas,E(160,160)); move.RespondToKeyDown(canvas,new KeyEventArgs(Keys.Menu));
                Check(move.RespondToKeyDown(canvas,new KeyEventArgs(Keys.Escape))==GH_ObjectResponse.Release,"Escape cancels native copy mode"); move.Destroy();
                Check(doc.Objects.Count==1 && p.Attributes.Pivot==new PointF(100,100),"Escape leaves the original at its initial position with no copy");
            }
            Check(typeof(Brand).GetField("Connect",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)==null && typeof(Brand).GetField("Duplicate",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)==null,"Operation icon resources have been removed");
            Check(Commands.Match(Keys.Alt|Keys.Q)=="duplicate" && Commands.Match(Keys.Alt|Keys.W)=="connect","Existing explicit operation shortcuts remain available");
            log.WriteLine(count+" regression checks passed.");
        }
        finally { canvas.Document=original; canvas.Invalidate(); }
    }
    sealed class MultiPortFixture : GH_Component
    {
        public MultiPortFixture() : base("Fixture","Fixture","Three inputs and outputs","Test","Test") {}
        public override Guid ComponentGuid { get { return new Guid("ba2c4058-2e85-4501-9aaf-6b84958d4829"); } }
        protected override void RegisterInputParams(GH_InputParamManager p) { for(int i=0;i<3;i++) p.AddNumberParameter("Input "+i,"I"+i,"",GH_ParamAccess.item); }
        protected override void RegisterOutputParams(GH_OutputParamManager p) { for(int i=0;i<3;i++) p.AddNumberParameter("Output "+i,"O"+i,"",GH_ParamAccess.item); }
        protected override void SolveInstance(IGH_DataAccess da) {}
    }
}
