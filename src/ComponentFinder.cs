using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace WireShelf
{
    public sealed class FinderEntry
    {
        public IGH_DocumentObject Object;
        public double Milliseconds;
        public bool HasTiming;
        public string Name { get { return Object.Name; } }
        public string NickName { get { return Object.NickName; } }
        public static List<FinderEntry> Read(GH_Document doc, string query, bool slowest)
        {
            if (doc == null) return new List<FinderEntry>();
            var words = (query ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var rows = doc.Objects.Where(o => o is IGH_Component || o is IGH_Param)
                .Where(o => words.All(word => (o.Name + " " + o.NickName + " " + o.Category + " " + o.SubCategory + " " + o.InstanceGuid)
                    .IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(o => new FinderEntry { Object = o, HasTiming = doc.Profiler == GH_ProfilerMode.Processor && ((IGH_ActiveObject)o).Phase == GH_SolutionPhase.Computed,
                    Milliseconds = Math.Max(0, ((IGH_ActiveObject)o).ProcessorTime.TotalMilliseconds) });
            return (slowest ? rows.OrderByDescending(r => r.Milliseconds).ThenBy(r => r.Name) : rows.OrderBy(r => r.Name).ThenBy(r => r.NickName)).ToList();
        }
        public static bool Navigate(GH_Canvas canvas, GH_Document document, IGH_DocumentObject target)
        {
            if (canvas == null || canvas.Document != document || !document.Objects.Contains(target)) return false;
            foreach (var obj in document.Objects) obj.Attributes.Selected = obj == target;
            canvas.Viewport.Zoom = 1.0F;
            canvas.Viewport.Focus(target.Attributes);
            canvas.Invalidate(); canvas.UpdateDocumentPreview();
            return true;
        }
    }

    internal sealed class ComponentFinder : UserControl
    {
        private readonly GH_Canvas canvas;
        private readonly TextBox search = new TextBox { Dock = DockStyle.Top, AccessibleName = "Search components by name, nickname or category" };
        private readonly CheckBox slow = new CheckBox { Text = "Slowest first", Checked = true, Dock = DockStyle.Top, Height = 30 };
        private readonly Button profiler = new Button { Text = "Enable Profiler", Dock = DockStyle.Top, Height = 30 };
        private readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, AccessibleName = "Search results and Profiler timings" };
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(5) };
        private GH_Document document;
        private string signature;
        internal ComponentFinder(GH_Canvas canvas)
        {
            this.canvas = canvas; Dock = DockStyle.Fill; Padding = new Padding(8);
            list.Columns.Add("Component", 155); list.Columns.Add("Alias", 80); list.Columns.Add("ms", 75, HorizontalAlignment.Right);
            list.Resize += delegate { list.Columns[2].Width = 70; list.Columns[1].Width = 70; list.Columns[0].Width = Math.Max(65, list.ClientSize.Width - 144); };
            Controls.Add(list); Controls.Add(slow); Controls.Add(profiler); Controls.Add(search); Controls.Add(status);
            profiler.Click += delegate { if (canvas.Document != null) { canvas.Document.Profiler = GH_ProfilerMode.Processor; RefreshResults(true); } };
            search.TextChanged += delegate { RefreshResults(true); };
            slow.CheckedChanged += delegate { RefreshResults(true); };
            list.SelectedIndexChanged += delegate {
                if (list.SelectedItems.Count == 1) FinderEntry.Navigate(canvas, document, (IGH_DocumentObject)list.SelectedItems[0].Tag);
            };
            search.KeyDown += delegate(object s, KeyEventArgs e) {
                if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Down) && list.Items.Count > 0)
                { list.Focus(); list.Items[0].Selected = true; e.SuppressKeyPress = true; }
            };
        }
        internal void FocusSearch() { search.Focus(); search.SelectAll(); }
        internal void RefreshResults(bool force)
        {
            var doc = canvas.Document;
            var rows = FinderEntry.Read(doc, search.Text, slow.Checked);
            var next = string.Join("|", rows.Select(r => r.Object.InstanceGuid + ":" + r.Name + ":" + r.NickName + ":" + r.HasTiming + ":" + r.Milliseconds.ToString("R", CultureInfo.InvariantCulture)));
            if (!force && document == doc && signature == next) return;
            document = doc; signature = next;
            profiler.Enabled = doc != null && doc.Profiler != GH_ProfilerMode.Processor;
            profiler.Text = doc != null && doc.Profiler == GH_ProfilerMode.Processor ? "Profiler enabled · measures on recompute" : "Enable Profiler";
            var selected = list.SelectedItems.Count == 1 ? list.SelectedItems[0].Tag : null;
            list.BeginUpdate(); list.Items.Clear();
            foreach (var row in rows)
            {
                var item = new ListViewItem(new[] { row.Name, row.NickName, row.HasTiming ? row.Milliseconds.ToString("0.000") : "—" }) { Tag = row.Object,
                    ToolTipText = row.Object.Category + " / " + row.Object.SubCategory + "\n" + row.Object.InstanceGuid };
                list.Items.Add(item);
                // Refreshing timings must not change selection or move the canvas.
                if (item.Tag == selected) item.Focused = true;
            }
            list.ShowItemToolTips = true; list.EndUpdate();
            status.Text = doc == null ? "Open a definition to find components." :
                rows.Count + " results · Last solution: " + doc.SolutionSpan.TotalMilliseconds.ToString("0.00") + " ms\n" +
                "Last measurement, including inputs. —: no current timing or pending recompute. Enable Profiler and recompute (F5) to update. Current definition level.";
        }
    }
}

