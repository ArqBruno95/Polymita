using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using Grasshopper.GUI.Canvas;
using WireShelf;

public static class ToolboxTests
{
    private static int passed;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
    [STAThread]
    public static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
            var name = new AssemblyName(e.Name).Name + ".dll";
            foreach (var folder in new[] { @"C:\Program Files\Rhino 8\Plug-ins\Grasshopper", @"C:\Program Files\Rhino 8\System" })
            { var path = Path.Combine(folder, name); if (File.Exists(path)) return Assembly.LoadFrom(path); }
            return null;
        };
        try { Run(); Console.WriteLine(passed + " toolbox checks passed."); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
    private static void Run()
    {
        foreach (var pair in new[] {
            new[] { new PointF(0,0), new PointF(150,70) }, new[] { new PointF(0,50), new PointF(150,50) },
            new[] { new PointF(150,0), new PointF(0,70) }, new[] { new PointF(150,50), new PointF(0,50) },
            new[] { new PointF(0,0), new PointF(0,0) }, new[] { new PointF(-500,-300), new PointF(-495,-310) } })
        {
            using (var path = WireStyles.Polyline(pair[0], pair[1]))
            {
                var points = path.PathPoints;
                Check(points[0] == pair[0] && points[points.Length - 1] == pair[1], "Polyline endpoints " + pair[0] + " → " + pair[1]);
                Check(points.Zip(points.Skip(1), (a,b) => a.X == b.X || a.Y == b.Y).All(x => x), "Only orthogonal segments");
                Check(path.PathTypes.All(t => t <= 1), "No Bezier segments");
            }
        }
        var root = Path.Combine(Path.GetTempPath(), "WireShelf-toolbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); var file = Path.Combine(root, "settings.json");
        var settings = ToolboxSettings.Load(file);
        Check(!settings.Polylines && !settings.Highlight, "New settings leave native rendering unchanged");
        settings.Polylines = true; settings.Highlight = true; settings.SelectedArgb = Color.Blue.ToArgb(); settings.View = "Top"; settings.PanelWidth = 500;
        settings.Save(file); var saved = ToolboxSettings.Load(file);
        Check(saved.Polylines && saved.Highlight && saved.SelectedArgb == Color.Blue.ToArgb() && saved.View == "Top", "Round-trip tools preferences");
        settings.PanelWidth = 10000; settings.Save(file);
        Check(ToolboxSettings.Load(file).PanelWidth == 900 && File.Exists(file + ".bak"), "Validated width and atomic backup");
        File.WriteAllText(file, "broken json"); bool rejected = false;
        try { ToolboxSettings.Load(file); } catch { rejected = true; }
        Check(rejected && File.ReadAllText(file) == "broken json", "Invalid settings preserved");
        // These files were created by this test only.
        Directory.Delete(root, true);
        Check(FinderEntry.Read(null, "", true).Count == 0, "Finder handles no document");
        Check(WireStyles.DistanceToPolyline(new PointF(0,0), new PointF(100,80), new PointF(100,40)) == 0, "Hit testing follows vertical segment");
        Check(WireStyles.DistanceToPolyline(new PointF(100,0), new PointF(0,0), new PointF(50,0)) == 0, "Hit testing follows backwards aligned wire");
        WireStyles.Variant=1;
        foreach(var end in new[] { new PointF(300,100),new PointF(300,-100),new PointF(-100,80),new PointF(0,100),new PointF(300,0),PointF.Empty }) {
            var k=WireStyles.Corners(PointF.Empty,end);
            Check(k.Length==4 && k[0]==PointF.Empty && k[3]==end && k[0].Y==k[1].Y && k[2].Y==k[3].Y,"Adaptive corners and horizontal ends");
            Check(end.X>0 || Math.Abs(k[1].X-k[0].X-26F)<0.001,"Backwards wire uses a short fixed stub");
            Check(WireStyles.DistanceToPolyline(k[0],k[3],new PointF((k[1].X+k[2].X)/2,(k[1].Y+k[2].Y)/2))<0.01,"Adaptive diagonal hit test");
            using(var p=WireStyles.Polyline(PointF.Empty,end)) {
                var pts=p.PathPoints;
                Check(pts[0]==PointF.Empty && pts[pts.Length-1]==end,"Filleted wire still meets both grips");
                Check(pts[1].Y==pts[0].Y && pts[pts.Length-2].Y==pts[pts.Length-1].Y,"Filleted wire leaves and arrives horizontally");
                Check(p.PathTypes.Any(t=>(t&7)==3),"Adaptive corners are filleted");
            }
        }
        var library=new ShelfLibrary(); var section=new ShelfSection { Title="Planos" }; library.Sections.Add(section);
        var favorite=new ShelfItem { ComponentId=Guid.NewGuid(),Name="Panel · lista 0–1",SnapshotXml="<data>mis datos</data>" }; section.Items.Add(favorite);
        var originalId=favorite.Id;
        Check(EnglishLibrary.Apply(library) && section.Title=="Planes" && favorite.Name=="Panel · list 0–1","English library migration");
        Check(favorite.Id==originalId && favorite.SnapshotXml=="<data>mis datos</data>","Migration preserves recipe payloads and IDs");
        section.Title="Mi sección";
        Check(!EnglishLibrary.Apply(library) && section.Title=="Mi sección","Language migration only runs once");
        WireStyles.Variant=0;
        Check(WireStyles.Closest(PointF.Empty, new PointF(10,0), new PointF(20,5)) == new PointF(10,0), "Hit testing clamps to segment endpoint");
    }
}
