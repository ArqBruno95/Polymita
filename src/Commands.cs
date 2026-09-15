using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Polymita
{
    // Every action the plug-in binds to a key, in one table, so the menu, the canvas
    // key handler, the Rhino-side keyboard hook and the editor all read the same
    // bindings and cannot drift apart.
    public static class Commands
    {
        public sealed class Entry
        {
            public readonly string Id, Title;
            public readonly Keys Fallback;
            internal Entry(string id, string title, Keys fallback) { Id=id; Title=title; Fallback=fallback; }
        }
        // Order is the order the menu lists them in: the tools that have a toolbar
        // icon first, in toolbar order, then the rest.
        public static readonly Entry[] All = new[] {
            new Entry("viewport","Rhino viewport",Keys.Control|Keys.Shift|Keys.V),
            new Entry("library","Edit library…",Keys.Control|Keys.Shift|Keys.B),
            new Entry("wires","Wire style and color",Keys.Control|Keys.Shift|Keys.W),
            new Entry("labels","Component and group labels",Keys.Control|Keys.Shift|Keys.L),
            new Entry("finder","Find in definition / Profiler",Keys.Control|Keys.Shift|Keys.F),
            new Entry("capture","Save selection as recipe",Keys.Control|Keys.Shift|Keys.R),
            new Entry("palette","Open favorites here",Keys.Control|Keys.Space),
            new Entry("connect","Connect selection",Keys.Alt|Keys.W),
            new Entry("duplicate","Duplicate selection",Keys.Alt|Keys.Q),
            new Entry("before","Data container before selection",Keys.Alt|Keys.A),
            new Entry("after","Data container after selection",Keys.Alt|Keys.D),
            new Entry("absorb","Add overlapping objects to group",Keys.Alt|Keys.G)
        };
        private static readonly Dictionary<string,Keys> bound = new Dictionary<string,Keys>();
        public static Keys Key(string id)
        {
            Keys value;
            if (bound.TryGetValue(id, out value)) return value;
            var entry = All.FirstOrDefault(e => e.Id == id);
            return entry == null ? Keys.None : entry.Fallback;
        }
        public static void Bind(string id, Keys combo) { bound[id] = combo; }
        // A binding that no longer names a command is dropped rather than rejected, so
        // a settings file written by a later build cannot stop the plug-in loading.
        public static void Load(string[] stored)
        {
            bound.Clear();
            if (stored == null) return;
            foreach (var line in stored)
            {
                if (String.IsNullOrEmpty(line)) continue;
                var split = line.IndexOf('=');
                if (split <= 0 || split == line.Length-1) continue;
                var id = line.Substring(0, split);
                if (!All.Any(e => e.Id == id)) continue;
                int value;
                if (Int32.TryParse(line.Substring(split+1), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    bound[id] = (Keys)value;
            }
        }
        public static string[] Save()
        { return All.Select(e => e.Id + "=" + ((int)Key(e.Id)).ToString(CultureInfo.InvariantCulture)).ToArray(); }
        // Which command a pressed combination runs, if any.
        public static string Match(Keys combo)
        {
            if (combo == Keys.None) return null;
            foreach (var entry in All) if (Key(entry.Id) == combo) return entry.Id;
            return null;
        }
        public static string Describe(Keys combo)
        {
            if (combo == Keys.None) return "—";
            var code = combo & Keys.KeyCode;
            if (code == Keys.None) return "—";
            var text = "";
            if ((combo & Keys.Control) != Keys.None) text += "Ctrl+";
            if ((combo & Keys.Shift) != Keys.None) text += "Shift+";
            if ((combo & Keys.Alt) != Keys.None) text += "Alt+";
            return text + (code == Keys.Space ? "Space" : code.ToString());
        }
    }
}
