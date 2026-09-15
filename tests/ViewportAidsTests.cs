using Rhino.ApplicationSettings;
using Polymita;
using System;
using System.IO;
using System.Linq;

// Runs inside Rhino. Every Rhino setting it touches is captured first and put back
// in the finally block, so running this suite leaves the user's modelling aids
// exactly as it found them.
public static class ViewportAidsTests
{
    static TextWriter log; static int count;
    static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); log.WriteLine("PASS " + message); log.Flush(); count++; }

    public static void Run(string root)
    {
        var folder = Path.Combine(root, "test-output");
        Directory.CreateDirectory(folder);
        var snapModes = ModelAidSettings.OsnapModes;
        var osnap = ModelAidSettings.Osnap;
        var project = ModelAidSettings.ProjectSnapToCPlane;
        using (log = new StreamWriter(Path.Combine(folder, "viewport-aids.txt"), false))
        {
            try
            {
                var snaps = ModelAids.Snaps();
                Check(snaps.Select(a => a.Text).SequenceEqual(new[] {
                        "End","Near","Point","Mid","Cen","Int","Perp","Tan","Quad","Knot","Vertex","Project","Disable" }),
                    "The Osnap bar carries Rhino's snaps under Rhino's names, in Rhino's order");
                var flags = snaps.Where(a => a.Mode != OsnapModes.None).Select(a => a.Mode).ToArray();
                Check(flags.Length == 11 && flags.Distinct().Count() == 11,
                    "Each snap button stands for one distinct Rhino snap");

                // Every snap reads and writes Rhino's own setting, both ways.
                ModelAids.Only(OsnapModes.End);
                foreach (var aid in snaps.Where(a => a.Mode != OsnapModes.None))
                {
                    var was = aid.On;
                    aid.On = !was;
                    Check(aid.On == !was && ((ModelAidSettings.OsnapModes & aid.Mode) == aid.Mode) == !was,
                        aid.Text + " reads and writes Rhino's own snap setting");
                    aid.On = was;
                    Check(aid.On == was, aid.Text + " returns to where Rhino had it");
                }
                // Rhino's right-click: that snap alone.
                ModelAids.Only(OsnapModes.Midpoint);
                Check(ModelAidSettings.OsnapModes == OsnapModes.Midpoint && ModelAidSettings.Osnap,
                    "Choosing one snap alone clears the rest and leaves the snaps running");

                var disable = snaps.Single(a => a.Text == "Disable");
                ModelAidSettings.Osnap = true;
                Check(!disable.On, "Disable is dark while the snaps are running");
                disable.On = true;
                Check(!ModelAidSettings.Osnap, "Disable suspends every snap at once");
                disable.On = false;
                Check(ModelAidSettings.Osnap, "Turning Disable off brings the snaps back");

                Check(ModelAids.Status().Select(a => a.Text).SequenceEqual(new[] {
                        "Grid Snap","Ortho","Planar","Osnap","SmartTrack","Gumball" }),
                    "The status bar carries Rhino's aids under Rhino's names, in Rhino's order");
                foreach (var aid in ModelAids.Status())
                {
                    var was = aid.On;
                    aid.On = !was;
                    Check(aid.On == !was, aid.Text + " is Rhino's own switch, not a copy");
                    aid.On = was;
                }

                var metres = ModelAids.Format(3.6042);
                Check(metres.Split(' ').Length == 2 && metres.Split(' ')[1].Length > 0,
                    "A distance is written as a number and the document's unit: " + metres);

                log.WriteLine(count + " viewport aid checks passed.");
            }
            finally
            {
                ModelAidSettings.OsnapModes = snapModes;
                ModelAidSettings.Osnap = osnap;
                ModelAidSettings.ProjectSnapToCPlane = project;
            }
        }
    }
}
