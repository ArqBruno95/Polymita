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
        public static float Angle = 45;

        public static GraphicsPath Polyline(PointF output, PointF input)
        {
            var path = new GraphicsPath();
            if (Variant == 0)
            {
                path.AddLines(new[] { output, new PointF(input.X, output.Y), input });
            }
            else
            {
                // Horizontal departure and arrival; the middle segment adapts to both grips.
                var dx = input.X-output.X;
                var lead = dx > 0 ? dx * 0.28F : Math.Max(24, Math.Abs(dx) * 0.28F);
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
        public static float DistanceToPolyline(PointF source, PointF target, PointF locus)
        {
            var result = float.MaxValue;
            using (var path = Polyline(source, target))
            {
                var points = path.PathPoints;
                for (int i = 1; i < points.Length; i++)
                {
                    var closest = Closest(points[i-1], points[i], locus);
                    var dx = closest.X - locus.X; var dy = closest.Y - locus.Y;
                    result = Math.Min(result, (float)Math.Sqrt(dx * dx + dy * dy));
                }
            }
            return result;
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
                var folder = Path.GetDirectoryName(typeof(WireStyles).Assembly.Location);
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "0Harmony")
                    ?? Assembly.LoadFrom(Path.Combine(folder, "runtimes", framework, "0Harmony.dll"));
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

