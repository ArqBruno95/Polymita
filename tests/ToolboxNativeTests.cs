using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using WireShelf;

public static class ToolboxNativeTests
{
    private static TextWriter log;
    private static int count;
    private static void Check(bool test, string message) { if (!test) throw new Exception(message); count++; log.WriteLine("PASS " + message); log.Flush(); }
    public static void Run(string root)
    {
        count = 0;
        using (log = new StreamWriter(Path.Combine(root, "test-output", "toolbox-integration.txt")))
        {
            log.WriteLine("Rhino " + Rhino.RhinoApp.Version + " / runtime " + Environment.Version);
            var canvas = Instances.ActiveCanvas;
            var original = canvas.Document;
            var view = canvas.Viewport.Duplicate();
            try
            {
                using (var doc = new GH_Document())
                {
                    doc.Profiler = GH_ProfilerMode.Processor;
                    var timer = new DelayComponent(); timer.CreateAttributes(); timer.NickName = "Prueba lenta"; timer.Attributes.Pivot = new PointF(1500,1000); doc.AddObject(timer, false);
                    var point = new Param_Point(); point.CreateAttributes(); doc.AddObject(point,false);
                    canvas.Document = doc;
                    doc.NewSolution(false);
                    log.WriteLine("Timer: " + timer.ProcessorTime.TotalMilliseconds + " ms, phase " + timer.Phase + ", solution " + doc.SolutionSpan.TotalMilliseconds + " ms");
                    Check(FinderEntry.Read(doc, "", true).Count == 2, "Finder includes components and standalone parameters");
                    Check(FinderEntry.Read(doc,"PRUEBA lenta",true).Single().Object == timer, "Finder matches all query words and nicknames case-insensitively");
                    Check(FinderEntry.Read(doc,"missing-name",true).Count == 0, "Finder handles no results");
                    Check(FinderEntry.Read(doc,timer.InstanceGuid.ToString(),true).Single().Object == timer, "Duplicate names disambiguated by instance ID");
                    Check(FinderEntry.Read(doc,"",true)[0].Milliseconds >= 7, "Profiler uses measured computation time");
                    Check(FinderEntry.Read(doc,"",true)[0].Object == timer, "Profiler sorts slowest component first");
                    canvas.Document = doc;
                    Check(FinderEntry.Navigate(canvas,doc,timer) && timer.Attributes.Selected && !point.Attributes.Selected, "Finder navigation selects the exact result");
                    Check(!FinderEntry.Navigate(canvas,original,timer), "Stale document results cannot navigate");
                    log.WriteLine("Canvas centering is verified interactively; a minimized host has no viewport extent.");
                    canvas.Document = original;
                }
                var a = GH_Skin.wire_selected_a; var b = GH_Skin.wire_selected_b;
                WireStyles.SetHighlight(true,Color.OrangeRed);
                Check(GH_Skin.wire_selected_a == Color.FromArgb(255,Color.OrangeRed) && GH_Skin.wire_selected_b == Color.FromArgb(255,Color.OrangeRed), "Both selected wire ends use chosen color");
                WireStyles.SetHighlight(false,Color.Empty);
                Check(GH_Skin.wire_selected_a == a && GH_Skin.wire_selected_b == b, "Selection colors restore exactly");
                WireStyles.SetPolylines(false); WireStyles.Variant=0; byte[] previous;
                using (var path = GH_Painter.ConnectionPath(new PointF(0,0),new PointF(100,80),GH_WireDirection.right,GH_WireDirection.left)) previous = path.PathTypes;
                WireStyles.SetPolylines(true);
                using (var path = GH_Painter.ConnectionPath(new PointF(0,0),new PointF(100,80),GH_WireDirection.right,GH_WireDirection.left))
                    Check(path.PathTypes.All(t => t <= 1) && path.PointCount == 3, "Two-segment renderer overrides the current native or third-party path");
                var method = typeof(GH_Document).GetMethod("DistanceToWire",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance);
                var distance = (float)method.Invoke(method.IsStatic ? null : original, new object[] { new PointF(100,40), 5F, new PointF(0,0), new PointF(100,80) });
                Check(distance < 0.01F, "Native hit test follows the visible polyline");
                WireStyles.SetPolylines(false);
                using (var path = GH_Painter.ConnectionPath(new PointF(0,0),new PointF(100,80),GH_WireDirection.right,GH_WireDirection.left))
                    Check(previous.SequenceEqual(path.PathTypes), "Disabling polylines restores prior renderer");
                using (var pane = new RhinoViewportPane(canvas, new ToolboxSettings()))
                {
                    var control = pane.ViewControl;
                    Check(!control.HasView, "Hidden panes do not create native Rhino windows");
                }
                log.WriteLine(count + " native toolbox checks passed.");
            }
            catch (Exception ex) { log.WriteLine(ex); throw; }
            finally { canvas.Document = original; canvas.Viewport.Set(view); WireStyles.Reset(); }
        }
    }
    private sealed class DelayComponent : GH_Component
    {
        public DelayComponent() : base("Timing fixture", "Timing", "Test only", "WireShelf Tests", "Tests") { }
        public override Guid ComponentGuid { get { return new Guid("c2d61bf5-b652-47c8-8a27-dbc839ccad06"); } }
        protected override void RegisterInputParams(GH_InputParamManager p) { }
        protected override void RegisterOutputParams(GH_OutputParamManager p) { p.AddNumberParameter("N","N","Test",GH_ParamAccess.item); }
        protected override void SolveInstance(IGH_DataAccess da) { Thread.Sleep(12); da.SetData(0,1); }
    }
}

