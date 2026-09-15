using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Polymita;

// Runs inside Rhino, on an isolated canvas and transient definitions. Does not
// replace Instances.ActiveCanvas, register a plugin or alter installed settings.
public static class Regression086Tests
{
    static TextWriter log; static int count;
    static void Check(bool ok,string message)
    { if(!ok)throw new Exception(message); log.WriteLine("PASS "+message); log.Flush(); count++; }
    static bool Near(float a,float b) { return Math.Abs(a-b)<0.01F; }
    static T Add<T>(GH_Document doc,T obj,float x,float y) where T:IGH_DocumentObject
    { obj.CreateAttributes();obj.Attributes.Pivot=new PointF(x,y);doc.AddObject(obj,false);obj.Attributes.ExpireLayout();obj.Attributes.PerformLayout();return obj; }
    static PointF Centre(IGH_DocumentObject o)
    { var b=o.Attributes.Bounds;return new PointF(b.Left+b.Width/2,b.Top+b.Height/2); }
    static GH_CanvasMouseEvent E(PointF p,MouseButtons button=MouseButtons.Left)
    { return new GH_CanvasMouseEvent(Point.Round(p),p,button,1,0); }
    static PointF Plus(PointF p,float x,float y) { return new PointF(p.X+x,p.Y+y); }
    static void Raise(GH_Canvas canvas,string name,EventArgs e)
    { typeof(GH_Canvas).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(canvas,new object[]{e}); }
    static void Click(GH_Canvas canvas,PointF point)
    {
        var p=canvas.Viewport.ProjectPoint(point);
        Raise(canvas,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,(int)p.X,(int)p.Y,0));
        Raise(canvas,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,(int)p.X,(int)p.Y,0));
    }
    static PointF GroupGrab(GH_Document doc,GH_Group group)
    {
        group.Attributes.ExpireLayout();group.Attributes.PerformLayout();var b=group.Attributes.Bounds;
        for(float y=b.Top;y<=b.Bottom;y+=2) for(float x=b.Left;x<=b.Right;x+=2)
        { var p=new PointF(x,y);var hit=doc.FindAttribute(p,true);if(hit!=null&&hit.GetTopLevel.DocObject==group)return p; }
        throw new Exception("No group pick region");
    }
    static Bitmap Render(GH_Canvas canvas,string path)
    {
        var bitmap=new Bitmap(canvas.Width,canvas.Height);
        canvas.DrawToBitmap(bitmap,canvas.ClientRectangle);bitmap.Save(path);
        return bitmap;
    }
    static int DifferentWirePixels(Bitmap a,Bitmap b)
    {
        int different=0;
        for(int y=80;y<280;y++)for(int x=210;x<560;x++)
        {
            var p=a.GetPixel(x,y);var q=b.GetPixel(x,y);
            // The native grid fades between paints. Ignore its small grey-level
            // change; a persistent selected wire changes channels by much more.
            if(Math.Abs(p.R-q.R)>8||Math.Abs(p.G-q.G)>8||Math.Abs(p.B-q.B)>8)different++;
        }
        return different;
    }
    public static void Run(string root)
    {
        count=0;
        using(log=new StreamWriter(Path.Combine(root,"test-output","regression-086.txt")))
        using(var canvas=new GH_Canvas())
        {
            canvas.Size=new Size(800,600); canvas.CreateControl(); canvas.Viewport.Zoom=1;
            using(var doc=new GH_Document())
            {
                canvas.Document=doc;
                var p=Add(doc,new Param_Number(),100,100);p.Attributes.Selected=true;
                var down=Centre(p);var before=p.Attributes.Pivot;
                var move=new AlignInteraction(canvas,E(down),CanvasOperations.ExpandSelection(doc));
                Check(!move.IsActive,"A press alone stays inactive like native GH_DragInteraction");
                move.RespondToMouseMove(canvas,E(Plus(down,1,1)));
                Check(!move.IsActive&&p.Attributes.Pivot==before,"Small click jitter does not move or activate the drag");
                move.RespondToMouseUp(canvas,E(down));
                Check(!move.IsActive,"Click release permits native object MouseUp handling");
                move.Destroy();move.Destroy();
                Check(p.Attributes.Pivot==before,"Repeated cleanup is safe and an ordinary click preserves position");

                move=new AlignInteraction(canvas,E(down),CanvasOperations.ExpandSelection(doc));
                move.RespondToMouseMove(canvas,E(Plus(down,60,40)));
                Check(move.IsActive&&p.Attributes.Pivot==Plus(before,60,40),"Actual movement activates and translates the component");
                var response=move.RespondToMouseMove(canvas,E(Plus(down,200,200),MouseButtons.None));
                Check(response==GH_ObjectResponse.Release&&p.Attributes.Pivot==Plus(before,60,40),"Missing MouseUp releases the interaction without jumping to the cursor");
                move.Destroy();doc.Undo();
                Check(p.Attributes.Pivot==before,"Interrupted movement retains one-step Undo");
                move=new AlignInteraction(canvas,E(down),CanvasOperations.ExpandSelection(doc));
                move.RespondToMouseMove(canvas,E(Plus(down,70,30)));move.Destroy();
                Check(p.Attributes.Pivot==before,"Cancelled movement restores original placement");

                using(var gestures=new CanvasGestures(canvas))
                {
                    for(int i=0;i<12;i++) { Click(canvas,down);Click(canvas,new PointF(600,400)); }
                    Check(canvas.ActiveInteraction==null&&!p.Attributes.Selected,"Repeated select/deselect clicks leave no blocked interaction or stale selection");
                    Click(canvas,down);
                    Check(p.Attributes.Selected&&canvas.ActiveInteraction==null,"The next component click still selects normally");
                    int redraws=0;canvas.Invalidated+=delegate { redraws++; };
                    doc.SelectAll();Raise(canvas,"OnKeyUp",new KeyEventArgs(Keys.Control|Keys.A));
                    Application.DoEvents();
                    Check(redraws>0,"Ctrl+A key release schedules a complete Canvas redraw");
                    redraws=0;Click(canvas,new PointF(600,400));Application.DoEvents();
                    Check(redraws>0&&!p.Attributes.Selected,"Deselection redraws the whole Canvas after native input completes");
                    p.Attributes.Selected=true;
                    move=new AlignInteraction(canvas,E(down),CanvasOperations.ExpandSelection(doc));
                    canvas.ActiveInteraction=move;
                    move.RespondToMouseMove(canvas,E(Plus(down,30,20)));
                    Raise(canvas,"OnMouseCaptureChanged",EventArgs.Empty);Application.DoEvents();
                    Check(canvas.ActiveInteraction==null,"Losing mouse capture also releases our drag");
                    doc.Undo();Check(p.Attributes.Pivot==before,"Capture loss keeps movement undoable");
                }
                canvas.Document=null;
            }
            using(var doc=new GH_Document())
            {
                canvas.Document=doc;
                var first=Add(doc,new Param_Number(),100,100);
                var grabbed=Add(doc,new Param_Number(),230,220);
                var target=Add(doc,new Param_Number(),600,300);
                first.Attributes.Selected=grabbed.Attributes.Selected=true;
                var fp=first.Attributes.Pivot;var gp=grabbed.Attributes.Pivot;var down=Centre(grabbed);
                var move=new AlignInteraction(canvas,E(down),CanvasOperations.ExpandSelection(doc));
                var y=target.Attributes.InputGrip.Y-down.Y-20;
                move.RespondToMouseMove(canvas,E(Plus(down,70,y)));
                Check(Near(Centre(grabbed).Y,target.Attributes.InputGrip.Y),"Multiple selection snaps the grabbed component, not the union of selection bounds");
                Check(Near(first.Attributes.Pivot.Y-fp.Y,grabbed.Attributes.Pivot.Y-gp.Y)&&Near(first.Attributes.Pivot.X-fp.X,grabbed.Attributes.Pivot.X-gp.X),"All selected components follow the identical translation");
                Check(target.Attributes.Pivot==new PointF(600,300),"Reference component remains stationary");
                move.RespondToMouseUp(canvas,E(Plus(down,70,y)));move.Destroy();doc.Undo();
                Check(first.Attributes.Pivot==fp&&grabbed.Attributes.Pivot==gp,"Multi-selection movement undoes together");
                canvas.Document=null;
            }
            using(var doc=new GH_Document())
            {
                canvas.Document=doc;
                var a=Add(doc,new Param_Number(),100,100);var b=Add(doc,new Param_Number(),200,180);
                var note=Add(doc,new GH_Scribble(),130,230);
                var inner=Add(doc,new GH_Group(),0,0);inner.AddObject(a.InstanceGuid);inner.AddObject(b.InstanceGuid);
                var group=Add(doc,new GH_Group(),0,0);group.AddObject(inner.InstanceGuid);group.AddObject(note.InstanceGuid);
                inner.Attributes.PerformLayout();group.Attributes.PerformLayout();
                var target=Add(doc,new Param_Number(),600,400);target.AddSource(a);
                group.Attributes.Selected=true;var ids=CanvasOperations.ExpandSelection(doc);var down=GroupGrab(doc,group);
                var anchors=new DragAnchors(doc,ids,doc.Objects.Where(o=>ids.Contains(o.InstanceGuid)).ToList(),ids,down,group.Attributes.Bounds);
                Check(anchors.Targets.Length==0,"Group-border drag does not borrow a member's stronger wire snap");
                var box=group.Attributes.Bounds;var ap=a.Attributes.Pivot;var bp=b.Attributes.Pivot;var np=note.Attributes.Pivot;
                var move=new AlignInteraction(canvas,E(down),ids);
                float dy=target.Attributes.Bounds.Top-box.Bottom-5;
                move.RespondToMouseMove(canvas,E(Plus(down,70,dy)));
                Check(Near(group.Attributes.Bounds.Bottom,target.Attributes.Bounds.Top),"Nested group outline snaps to a stationary object edge");
                Check(Near(a.Attributes.Pivot.Y-ap.Y,b.Attributes.Pivot.Y-bp.Y)&&Near(a.Attributes.Pivot.Y-ap.Y,note.Attributes.Pivot.Y-np.Y),"Nested components and annotations retain their relative positions");
                move.RespondToMouseUp(canvas,E(Plus(down,70,dy)));move.Destroy();doc.Undo();
                Check(a.Attributes.Pivot==ap&&b.Attributes.Pivot==bp&&note.Attributes.Pivot==np,"Grouped movement with annotations has one-step Undo");
                using(var gestures=new CanvasGestures(canvas))
                {
                    var screen=canvas.Viewport.ProjectPoint(down);
                    Raise(canvas,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,(int)screen.X,(int)screen.Y,0));
                    Check(canvas.ActiveInteraction is AlignInteraction,"Native group drag containing a scribble receives Polymita snaps");
                    canvas.ActiveInteraction=null;
                }
                canvas.Document=null;
            }
            using(var doc=new GH_Document())
            using(var gestures=new CanvasGestures(canvas))
            {
                canvas.Document=doc;
                var a=Add(doc,new Param_Number(),100,100);var b=Add(doc,new Param_Number(),650,230);b.AddSource(a);
                canvas.Viewport.Zoom=1;
                using(var before=Render(canvas,Path.Combine(root,"test-output","wires-before.png")))
                {
                    doc.SelectAll();Raise(canvas,"OnKeyUp",new KeyEventArgs(Keys.Control|Keys.A));Application.DoEvents();
                    using(var selected=Render(canvas,Path.Combine(root,"test-output","wires-selected.png")))
                    {
                        Check(DifferentWirePixels(before,selected)>0,"Select All visibly changes the wire colour");
                        Click(canvas,new PointF(400,500));Application.DoEvents();
                        Check(!a.Attributes.Selected&&!b.Attributes.Selected,"Empty-canvas click deselects both wire endpoints");
                        using(var after=Render(canvas,Path.Combine(root,"test-output","wires-deselected.png")))
                            Check(DifferentWirePixels(before,after)==0,"After deselection every sampled wire pixel returns to its original colour");
                    }
                }
                canvas.Document=null;
            }
            Regression085Tests.RunOnCanvas(root,canvas);
            log.WriteLine(count+" native Canvas regression checks passed.");
        }
    }
}
