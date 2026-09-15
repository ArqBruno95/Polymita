using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System.Collections.Generic;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Polymita
{
    public static class WireStyles
    {
        private static object harmony;
        private static readonly Dictionary<MethodInfo, MethodInfo> patches = new Dictionary<MethodInfo, MethodInfo>();
        public static bool Polylines { get; private set; }
        public static int Variant;
        // Why the patch could not be installed, for the Wires tab to report on demand.
        public static string LastFailure;

        // Horizontal departure and arrival; the middle segment adapts to both grips.
        // Reaching backwards the stub is a short fixed length rather than a share of the
        // span: a proportional one grows with the distance and turns the rounded hook
        // into a long thin spike, instead of the tight turn a relay wire makes.
        private static float Lead(PointF output, PointF input)
        {
            var dx = input.X-output.X;
            return dx > 0 ? dx * 0.28F : 26F;
        }
        // Radius of the rounded corners on the adaptive wire, in canvas units.
        public static float Fillet = 11F;
        // The corners the wire turns through, ends included. Both variants leave and
        // arrive horizontally; when the input sits left of the output the lead pushes
        // outwards, which is what turns the adaptive wire into a straight run with a
        // rounded hook at each end.
        public static PointF[] Corners(PointF output, PointF input)
        {
            if (Variant == 0) return new[] { output, new PointF(input.X, output.Y), input };
            var lead = Lead(output, input);
            return new[] { output, new PointF(output.X+lead,output.Y), new PointF(input.X-lead,input.Y), input };
        }
        private static PointF Toward(PointF from, PointF to, float distance)
        {
            var dx = to.X - from.X; var dy = to.Y - from.Y;
            var length = (float)Math.Sqrt(dx*dx + dy*dy);
            if (length <= 0.0001F) return from;
            var step = Math.Min(distance, length * 0.45F);
            return new PointF(from.X + dx/length*step, from.Y + dy/length*step);
        }
        // A quadratic through the corner, written as a cubic. It meets both segments
        // tangentially like a fillet and stays valid however short the adjoining
        // segments get, which an arc of fixed radius does not.
        private static void Bend(GraphicsPath path, PointF enter, PointF corner, PointF leave)
        {
            path.AddBezier(enter,
                new PointF(enter.X + (corner.X-enter.X)*2F/3F, enter.Y + (corner.Y-enter.Y)*2F/3F),
                new PointF(leave.X + (corner.X-leave.X)*2F/3F, leave.Y + (corner.Y-leave.Y)*2F/3F),
                leave);
        }
        // Built without the corner array: with segmented wires on by default this runs
        // for every wire on the canvas on every repaint.
        public static GraphicsPath Polyline(PointF output, PointF input)
        {
            var path = new GraphicsPath();
            if (Variant == 0)
            {
                var corner = new PointF(input.X, output.Y);
                path.AddLine(output, corner); path.AddLine(corner, input);
                return path;
            }
            var lead = Lead(output, input);
            var first = new PointF(output.X+lead, output.Y);
            var second = new PointF(input.X-lead, input.Y);
            var enterFirst = Toward(first, output, Fillet);
            var leaveFirst = Toward(first, second, Fillet);
            // Measured from where the first bend released, so two fillets can never
            // overrun each other on a short middle segment.
            var enterSecond = Toward(second, leaveFirst, Fillet);
            var leaveSecond = Toward(second, input, Fillet);
            path.AddLine(output, enterFirst);
            Bend(path, enterFirst, first, leaveFirst);
            path.AddLine(leaveFirst, enterSecond);
            Bend(path, enterSecond, second, leaveSecond);
            path.AddLine(leaveSecond, input);
            return path;
        }

        // Signature matches the public GH_Painter API. All native pen and data-tree styling stays in GH.
        public static void PathPostfix(ref GraphicsPath __result, PointF pointA, PointF pointB,
            GH_WireDirection directionA, GH_WireDirection directionB)
        {
            if (!Polylines || directionA == directionB) return;
            if (__result != null) __result.Dispose();
            if (directionA == GH_WireDirection.right) __result = Polyline(pointA, pointB);
            else { __result = Polyline(pointB, pointA); __result.Reverse(); }
        }

        public static PointF Closest(PointF a, PointF b, PointF p)
        {
            var dx = b.X - a.X; var dy = b.Y - a.Y; var length = dx * dx + dy * dy;
            var t = length <= 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X-a.X)*dx + (p.Y-a.Y)*dy) / length));
            return new PointF(a.X + t * dx, a.Y + t * dy);
        }
        public static void DistancePostfix(ref float __result, PointF locus, float radius, PointF source, PointF target)
        {
            if (!Polylines) return;
            __result = DistanceToPolyline(source, target, locus);
        }
        private static float Segment(PointF a, PointF b, PointF locus)
        {
            var closest = Closest(a, b, locus);
            var dx = closest.X - locus.X; var dy = closest.Y - locus.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
        // Grasshopper hit-tests every wire on the canvas as the pointer moves, so this
        // walks the same corners as Polyline without building a GraphicsPath per wire
        // and per move, which is what the patched hit test used to spend its time on.
        public static float DistanceToPolyline(PointF source, PointF target, PointF locus)
        {
            if (Variant == 0)
            {
                var corner = new PointF(target.X, source.Y);
                return Math.Min(Segment(source, corner, locus), Segment(corner, target, locus));
            }
            var lead = Lead(source, target);
            var a = new PointF(source.X+lead, source.Y);
            var b = new PointF(target.X-lead, target.Y);
            return Math.Min(Segment(source, a, locus), Math.Min(Segment(a, b, locus), Segment(b, target, locus)));
        }

        public static void SetPolylines(bool enabled)
        {
            if (enabled == Polylines) return;
            if (enabled)
            {
                var original = typeof(GH_Painter).GetMethod("ConnectionPath", new[] { typeof(PointF), typeof(PointF), typeof(GH_WireDirection), typeof(GH_WireDirection) });
                var distance = typeof(GH_Document).GetMethod("DistanceToWire", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, new[] { typeof(PointF), typeof(float), typeof(PointF), typeof(PointF) }, null);
                if (original == null) throw new NotSupportedException("This Grasshopper version does not expose a compatible wire path method.");
                if (distance == null) throw new NotSupportedException("No compatible wire hit-test method was found in this Grasshopper version.");
                var framework = Environment.Version.Major >= 8 ? "net8.0" : Environment.Version.Major >= 7 ? "net7.0" : "net48";
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "0Harmony")
                    ?? LoadHarmony(framework);
                var type = assembly.GetType("HarmonyLib.Harmony", true);
                // A reversible postfix also works when WiresRenderer supplies the original path.
                // Its patches remain installed; disabling this option immediately restores its result.
                harmony = Activator.CreateInstance(type, new object[] { "org.polymita.polylines" });
                Polylines = true;
                try
                {
                    foreach (var pair in new[] { new { Original = original, Hook = "PathPostfix" }, new { Original = distance, Hook = "DistancePostfix" } })
                    {
                        var hook = typeof(WireStyles).GetMethod(pair.Hook);
                        var hm = Activator.CreateInstance(assembly.GetType("HarmonyLib.HarmonyMethod", true), new object[] { hook });
                        type.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5).Invoke(harmony, new[] { pair.Original, null, hm, null, null });
                        patches[pair.Original] = hook;
                    }
                }
                catch { Polylines = false; RemovePatches(); throw; }
            }
            else
            {
                Polylines = false;
                RemovePatches();
            }
            Redraw();
        }
        // Polymita installs as a single .gha, so the Harmony runtimes travel inside it.
        // A copy sitting next to the assembly still wins, which keeps the packaged folder
        // layout of earlier releases working exactly as before.
        private static Assembly LoadHarmony(string framework)
        {
            var location = typeof(WireStyles).Assembly.Location;
            if (!String.IsNullOrEmpty(location))
            {
                var path = Path.Combine(Path.GetDirectoryName(location), "runtimes", framework, "0Harmony.dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            byte[] bytes;
            using (var stream = typeof(WireStyles).Assembly.GetManifestResourceStream("Polymita.runtimes." + framework + ".0Harmony.dll"))
            {
                if (stream == null)
                    throw new NotSupportedException("This build of Polymita does not carry the Harmony runtime for " + framework + ".");
                bytes = new byte[stream.Length];
                for (var read = 0; read < bytes.Length; )
                {
                    var step = stream.Read(bytes, read, bytes.Length - read);
                    if (step <= 0) throw new IOException("The embedded Harmony runtime could not be read.");
                    read += step;
                }
            }
            // Harmony rewrites methods at run time and expects a file behind its own
            // assembly; loaded straight from memory it has no location to work from and
            // the patch fails, which is what left both wire styles unavailable. Unpack
            // the carried copy beside the settings once and load it from there.
            try
            {
                var folder = Path.Combine(ShelfRuntime.Storage.DirectoryPath, "runtimes", framework);
                var unpacked = Path.Combine(folder, "0Harmony.dll");
                if (!File.Exists(unpacked) || new FileInfo(unpacked).Length != bytes.Length)
                {
                    Directory.CreateDirectory(folder);
                    var temp = unpacked + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    File.WriteAllBytes(temp, bytes);
                    if (File.Exists(unpacked)) File.Delete(unpacked);
                    File.Move(temp, unpacked);
                }
                return Assembly.LoadFrom(unpacked);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (NotSupportedException) { }
            // Nowhere writable: try it from memory rather than give up on the feature.
            harmony0 = Assembly.Load(bytes);
            if (resolver == null)
            {
                // Loaded from memory: a request by name has no file on disk to find.
                resolver = delegate(object sender, ResolveEventArgs e)
                { return new AssemblyName(e.Name).Name == "0Harmony" ? harmony0 : null; };
                AppDomain.CurrentDomain.AssemblyResolve += resolver;
            }
            return harmony0;
        }
        private static Assembly harmony0;
        private static ResolveEventHandler resolver;
        private static void RemovePatches()
        {
            if (harmony != null) foreach (var pair in patches)
                harmony.GetType().GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) }).Invoke(harmony, new object[] { pair.Key, pair.Value });
            patches.Clear(); harmony = null;
        }
        // Polymita draws the path a wire follows and nothing else. Wire colour belongs
        // to Grasshopper: the plug-in neither reads nor writes GH_Skin, so selected and
        // unselected wires keep whatever colour the running Grasshopper gives them.
        public static void Reset() { SetPolylines(false); }
        private static void Redraw() { if (Grasshopper.Instances.ActiveCanvas != null) Grasshopper.Instances.ActiveCanvas.Invalidate(); }
    }
}

