using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System.Collections.Generic;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WireShelf
{
    public static class WireStyles
    {
        private static object harmony;
        private static readonly Dictionary<MethodInfo, MethodInfo> patches = new Dictionary<MethodInfo, MethodInfo>();
        private static Color beforeA, beforeB, applied;
        private static bool colorsOwned;
        public static bool Polylines { get; private set; }
        public static int Variant;

        // Horizontal departure and arrival; the middle segment adapts to both grips.
        private static float Lead(PointF output, PointF input)
        {
            var dx = input.X-output.X;
            return dx > 0 ? dx * 0.28F : Math.Max(24, Math.Abs(dx) * 0.28F);
        }
        public static GraphicsPath Polyline(PointF output, PointF input)
        {
            var path = new GraphicsPath();
            if (Variant == 0)
            {
                path.AddLines(new[] { output, new PointF(input.X, output.Y), input });
            }
            else
            {
                var lead = Lead(output, input);
                path.AddLines(new[] { output, new PointF(output.X+lead,output.Y),
                    new PointF(input.X-lead,input.Y), input });
            }
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
                harmony = Activator.CreateInstance(type, new object[] { "org.wireshelf.polylines" });
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
            using (var stream = typeof(WireStyles).Assembly.GetManifestResourceStream("Polymita.runtimes." + framework + ".0Harmony.dll"))
            {
                if (stream == null)
                    throw new NotSupportedException("This build of Polymita does not carry the Harmony runtime for " + framework + ".");
                var bytes = new byte[stream.Length];
                for (var read = 0; read < bytes.Length; )
                {
                    var step = stream.Read(bytes, read, bytes.Length - read);
                    if (step <= 0) throw new IOException("The embedded Harmony runtime could not be read.");
                    read += step;
                }
                harmony0 = Assembly.Load(bytes);
            }
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
        public static void SetHighlight(bool enabled, Color color)
        {
            if (enabled)
            {
                if (!colorsOwned) { beforeA = GH_Skin.wire_selected_a; beforeB = GH_Skin.wire_selected_b; colorsOwned = true; }
                applied = Color.FromArgb(255, color); GH_Skin.wire_selected_a = applied; GH_Skin.wire_selected_b = applied;
            }
            else if (colorsOwned)
            {
                if (GH_Skin.wire_selected_a == applied) GH_Skin.wire_selected_a = beforeA;
                if (GH_Skin.wire_selected_b == applied) GH_Skin.wire_selected_b = beforeB;
                colorsOwned = false;
            }
            Redraw();
        }
        public static void Reset() { SetPolylines(false); SetHighlight(false, Color.Empty); }
        private static void Redraw() { if (Grasshopper.Instances.ActiveCanvas != null) Grasshopper.Instances.ActiveCanvas.Invalidate(); }
    }
}

