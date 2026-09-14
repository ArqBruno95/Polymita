using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WireShelf
{
    internal sealed class Editor : Form
    {
        private ShelfLibrary draft;
        private readonly TreeView tree = new TreeView();
        // Keyed by the image list index so a component resolves once per editor.
        private readonly ImageList thumbnails = new ImageList { ImageSize = new Size(20,20), ColorDepth = ColorDepth.Depth32Bit };
        private readonly Dictionary<Guid,int> thumbnailIndex = new Dictionary<Guid,int>();
        private readonly Label details = new Label();
        private bool dirty;
        internal Editor(ShelfItem captured = null)
        {
            draft = LibraryStore.Copy(ShelfRuntime.Library);
            Ui.Style(this, "Polymita · Edit library", new Size(850, 605));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var title = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Ui.Ink,
                Text = "Your library, your way\n", AutoSize = false };
            var subtitle = new Label { Text = "Organize sections and favorites. Save configured components from the canvas.",
                Dock = DockStyle.Bottom, Height = 27, Font = Font, ForeColor = Ui.Muted };
            title.Controls.Add(subtitle);
            var tools = Ui.Bar(Ui.Button("+ Section", AddSection), Ui.Button("+ Component", AddComponent),
                Ui.Button("+ Operation", AddOperation), Ui.Button("Capture selection", CaptureSelected), Ui.Button("Rename", Rename),
                Ui.Button("↑", () => MoveItem(-1)), Ui.Button("↓", () => MoveItem(1)), Ui.Button("Move to…", MoveTo), Ui.Button("Delete", Delete));
            tree.Dock = DockStyle.Fill; tree.HideSelection = false; tree.FullRowSelect = true; tree.ItemHeight = 30;
            tree.ImageList = thumbnails;
            tree.BorderStyle = BorderStyle.FixedSingle; tree.AccessibleName = "Sections and favorites, in display order";
            tree.AfterSelect += delegate { ShowDetails(); };
            // EditItem instantiates the favorite to list its ports, which throws when the
            // component is not installed; unguarded that would surface as a crash.
            tree.NodeMouseDoubleClick += delegate { if (SelectedItem != null) Ui.Safe(EditItem); };
            tree.KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.F2) Ui.Safe(Rename);
                if (e.Alt && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)) { Ui.Safe(() => MoveItem(e.KeyCode == Keys.Up ? -1 : 1)); e.Handled = true; }
            };
            details.Dock = DockStyle.Fill; details.Padding = new Padding(4, 10, 0, 0); details.ForeColor = Ui.Muted;
            var bottom = Ui.Bar(Ui.Button("Properties…", EditItem), Ui.Button("Update recipe", ReplaceRecipe),
                Ui.Button("Import…", Import), Ui.Button("Export…", Export), Ui.Button("Save", Save, true),
                Ui.Button("Close", Close));
            layout.Controls.Add(title, 0, 0); layout.Controls.Add(tools, 0, 1); layout.Controls.Add(tree, 0, 2);
            layout.Controls.Add(details, 0, 3); layout.Controls.Add(bottom, 0, 4); Controls.Add(layout);
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!dirty) return;
                var result = MessageBox.Show(this, "Save library changes before closing?", "Polymita",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel) e.Cancel = true;
                else if (result == DialogResult.Yes) try { Save(); } catch (Exception ex) { Ui.Error(ex); e.Cancel = true; }
            };
            FormClosed += delegate { thumbnails.Dispose(); };
            RefreshTree(null);
            if (captured != null) AddItem(captured);
        }
        private ShelfItem SelectedItem { get { return tree.SelectedNode == null ? null : tree.SelectedNode.Tag as ShelfItem; } }
        private ShelfSection SelectedSection
        {
            get
            {
                var node = tree.SelectedNode;
                if (node == null) return draft.Sections.FirstOrDefault();
                return (node.Tag as ShelfSection) ?? (node.Parent == null ? null : node.Parent.Tag as ShelfSection);
            }
        }
        private void RefreshTree(object selection)
        {
            tree.BeginUpdate(); tree.Nodes.Clear();
            foreach (var s in draft.Sections)
            {
                var group = new TreeNode(s.Title) { Tag = s, ForeColor = Ui.Accent };
                tree.Nodes.Add(group);
                if (ReferenceEquals(selection, s)) tree.SelectedNode = group;
                foreach (var item in s.Items)
                {
                    var node = new TreeNode(item.Name + (item.IsRecipe ? "   · recipe" : "")) { Tag = item };
                    var image = Thumbnail(item);
                    node.ImageIndex = node.SelectedImageIndex = image;
                    group.Nodes.Add(node); if (ReferenceEquals(selection, item)) tree.SelectedNode = node;
                }
                group.Expand();
            }
            tree.EndUpdate(); if (tree.SelectedNode == null && tree.Nodes.Count > 0) tree.SelectedNode = tree.Nodes[0];
            ShowDetails();
        }
        // -1 leaves the row without an image, which is what a section heading wants.
        private int Thumbnail(ShelfItem item)
        {
            if (item.IsAction)
            {
                var key = item.ActionId == "connect" ? new Guid("00000000-0000-0000-0000-0000000000c0")
                    : new Guid("00000000-0000-0000-0000-0000000000d0");
                return Register(key, item.ActionId == "connect" ? Brand.Connect : Brand.Duplicate);
            }
            var proxy = Instances.ComponentServer.EmitObjectProxy(item.ComponentId);
            return Register(item.ComponentId, proxy == null ? null : proxy.Icon);
        }
        private int Register(Guid key, Image icon)
        {
            int index;
            if (thumbnailIndex.TryGetValue(key, out index)) return index;
            if (icon == null) { thumbnailIndex[key] = -1; return -1; }
            thumbnails.Images.Add(icon);
            index = thumbnails.Images.Count - 1; thumbnailIndex[key] = index; return index;
        }
        private void ShowDetails()
        {
            var item = SelectedItem;
            details.Text = item == null ? "Sections appear as headings in the palette.\nUse ↑ / ↓ or Alt + arrow keys to reorder."
                : item.IsAction ? item.Name + " · Selection operation\n" + item.Notes
                : item.Name + (item.IsRecipe ? " · Saved native configuration" : " · Standard component") + "\n" + item.Notes +
                    "\nPreferred input: " + (item.InputIndex + 1) + "   ·   Preferred output: " + (item.OutputIndex + 1);
        }
        private void AddSection()
        {
            var title = Ui.Ask(this, "New section", "", 80); if (title == null) return;
            var section = new ShelfSection { Title = title }; draft.Sections.Add(section); dirty = true; RefreshTree(section);
        }
        private void AddComponent()
        {
            using (var picker = new Catalog()) if (picker.ShowDialog(this) == DialogResult.OK) AddItem(picker.Result);
        }
        private void AddItem(ShelfItem item)
        {
            var section = SelectedSection;
            if (section == null) { section = new ShelfSection(); draft.Sections.Add(section); }
            section.Items.Add(item); dirty = true; RefreshTree(item);
        }
        private void AddOperation()
        {
            using(var dialog=new Form()) {
                Ui.Style(dialog,"Add operation",new Size(410,150));
                var combo=new ComboBox { Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList };
                combo.Items.AddRange(new object[] { "Connect selection · Alt+W", "Duplicate selection · Alt+Q" }); combo.SelectedIndex=0;
                var bar=Ui.Bar(Ui.Button("Add",delegate { dialog.DialogResult=DialogResult.OK; },true)); bar.Dock=DockStyle.Bottom;
                dialog.Controls.Add(combo); dialog.Controls.Add(bar);
                if(dialog.ShowDialog(this)==DialogResult.OK) AddItem(new ShelfItem { ActionId=combo.SelectedIndex==0?"connect":"duplicate",Name=combo.Text,Notes="Runs on the current canvas selection." });
            }
        }
        private void CaptureSelected() { AddItem(ShelfRuntime.CaptureSelection()); }
        private void Rename()
        {
            var item = SelectedItem; var section = SelectedSection;
            if (section == null) return;
            var name = Ui.Ask(this, item == null ? "Section title" : "Favorite name", item == null ? section.Title : item.Name, item == null ? 80 : 120);
            if (name == null) return;
            if (item == null) section.Title = name; else item.Name = name;
            dirty = true; RefreshTree((object)item ?? section);
        }
        private void MoveItem(int offset)
        {
            var item = SelectedItem; var section = SelectedSection;
            if (section == null) return;
            if (item == null)
            {
                var i = draft.Sections.IndexOf(section); var j = i + offset;
                if (j < 0 || j >= draft.Sections.Count) return;
                draft.Sections.RemoveAt(i); draft.Sections.Insert(j, section);
            }
            else
            {
                var i = section.Items.IndexOf(item); var j = i + offset;
                if (j < 0 || j >= section.Items.Count) return;
                section.Items.RemoveAt(i); section.Items.Insert(j, item);
            }
            dirty = true; RefreshTree((object)item ?? section);
        }
        private void MoveTo()
        {
            var item = SelectedItem; var source = SelectedSection; if (item == null) return;
            using (var dialog = new Form())
            {
                Ui.Style(dialog, "Move favorite to section", new Size(360, 130));
                var combo = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
                foreach (var section in draft.Sections) combo.Items.Add(section); combo.SelectedItem = source;
                var ok = Ui.Button("Move", delegate { dialog.DialogResult = DialogResult.OK; }, true);
                var bar = Ui.Bar(ok); bar.Dock = DockStyle.Bottom; dialog.Controls.Add(combo); dialog.Controls.Add(bar);
                if (dialog.ShowDialog(this) != DialogResult.OK || combo.SelectedItem == source) return;
                source.Items.Remove(item); ((ShelfSection)combo.SelectedItem).Items.Add(item); dirty = true; RefreshTree(item);
            }
        }
        private void Delete()
        {
            var item = SelectedItem; var section = SelectedSection; if (section == null) return;
            if (item != null) section.Items.Remove(item);
            else
            {
                if (section.Items.Count > 0 && MessageBox.Show(this, "Delete this section and its " + section.Items.Count + " favorites?",
                    "Polymita", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                draft.Sections.Remove(section);
            }
            dirty = true; RefreshTree(null);
        }
        private void EditItem()
        {
            var item = SelectedItem; if (item == null) return;
            using (var dialog = new ItemProperties(item))
                if (dialog.ShowDialog(this) == DialogResult.OK) { dirty = true; RefreshTree(item); }
        }
        private void ReplaceRecipe()
        {
            var item = SelectedItem; if (item == null) return;
            if(item.IsAction) throw new InvalidOperationException("An operation does not contain a component recipe.");
            var captured = ShelfRuntime.CaptureSelection();
            if (captured.ComponentId != item.ComponentId) throw new InvalidOperationException("Select the same component type as this favorite.");
            item.SnapshotXml = captured.SnapshotXml; dirty = true; RefreshTree(item);
        }
        private void Save() { ShelfRuntime.Save(draft); dirty = false; Text = "Polymita · Library saved"; }
        private void Import()
        {
            using (var picker = new OpenFileDialog { Filter = "Polymita library (*.wireshelf.json)|*.wireshelf.json|JSON (*.json)|*.json" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                var incoming = LibraryStore.Read(picker.FileName);
                foreach (var section in incoming.Sections)
                {
                    section.Id = Guid.NewGuid(); foreach (var item in section.Items) item.Id = Guid.NewGuid();
                }
                var candidate = LibraryStore.Copy(draft); candidate.Sections.AddRange(incoming.Sections);
                LibraryStore.Validate(candidate); draft = candidate; dirty = true; RefreshTree(null);
            }
        }
        private void Export()
        {
            using (var picker = new SaveFileDialog { Filter = "Polymita library (*.wireshelf.json)|*.wireshelf.json", FileName = "My library.wireshelf.json" })
                if (picker.ShowDialog(this) == DialogResult.OK) new LibraryStore(picker.FileName).Save(draft);
        }
    }

    internal sealed class Catalog : Form
    {
        private readonly ListBox list = new ListBox();
        private readonly TextBox search = new SearchBox { Hint = "Search by name or category…" };
        internal ShelfItem Result;
        private sealed class Entry
        {
            internal IGH_ObjectProxy Proxy;
            internal string Label;
            public override string ToString() { return Label; }
        }
        private readonly Entry[] entries;
        internal Catalog()
        {
            Ui.Style(this, "Add installed component", new Size(615, 465));
            // Built once: the label was rebuilt for every proxy on every keystroke and
            // again for every rendered row, thousands of concatenations per character.
            entries = Instances.ComponentServer.ObjectProxies.Where(p => !p.Obsolete && p.Exposure != GH_Exposure.hidden)
                .OrderBy(p => p.Desc.Name)
                .Select(p => new Entry { Proxy = p, Label = p.Desc.Name + "   /   " + p.Desc.Category + " · " + p.Desc.SubCategory }).ToArray();
            search.Dock = DockStyle.Top; search.AccessibleName = "Search installed components";
            list.Dock = DockStyle.Fill; list.IntegralHeight = false; list.HorizontalScrollbar = true; list.ItemHeight = 24;
            var add = Ui.Button("Add", Pick, true); var bar = Ui.Bar(add); bar.Dock = DockStyle.Bottom;
            Controls.Add(list); Controls.Add(search); Controls.Add(bar); Padding = new Padding(14);
            search.TextChanged += delegate { Filter(); }; list.DoubleClick += delegate { Ui.Safe(Pick); };
            AcceptButton = add; Shown += delegate { search.Focus(); }; Filter();
        }
        private void Filter()
        {
            list.BeginUpdate(); list.Items.Clear();
            var term = search.Text.Trim();
            foreach (var entry in entries) if (entry.Label.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0) list.Items.Add(entry);
            list.EndUpdate(); if (list.Items.Count > 0) list.SelectedIndex = 0;
        }
        private void Pick()
        {
            var entry = list.SelectedItem as Entry; if (entry == null) return;
            var obj = entry.Proxy.CreateInstance();
            if (!(obj is IGH_Component) && !(obj is IGH_Param)) throw new InvalidOperationException("Choose a component or parameter with ports.");
            Result = new ShelfItem { ComponentId = entry.Proxy.Guid, Name = entry.Proxy.Desc.Name, Notes = entry.Proxy.Desc.Category + " / " + entry.Proxy.Desc.SubCategory };
            DialogResult = DialogResult.OK;
        }
    }

    internal sealed class ItemProperties : Form
    {
        internal ItemProperties(ShelfItem item)
        {
            Ui.Style(this, "Favorite properties", new Size(490, 330));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var name = new TextBox { Text = item.Name, MaxLength = 120, Dock = DockStyle.Fill };
            var notes = new TextBox { Text = item.Notes, MaxLength = 2000, Dock = DockStyle.Fill, Multiline = true, Height = 90 };
            var input = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            var output = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            if(!item.IsAction) {
                var obj = Recipes.Create(item);
                foreach (var p in Recipes.Ports(obj, false)) input.Items.Add((input.Items.Count + 1) + " · " + p.Name);
                foreach (var p in Recipes.Ports(obj, true)) output.Items.Add((output.Items.Count + 1) + " · " + p.Name);
            } else { input.Enabled=false; output.Enabled=false; }
            if (input.Items.Count > 0) input.SelectedIndex = Math.Min(item.InputIndex, input.Items.Count - 1);
            if (output.Items.Count > 0) output.SelectedIndex = Math.Min(item.OutputIndex, output.Items.Count - 1);
            var labels = new[] { "Name", "Notes / search", "Preferred input", "Preferred output" };
            var controls = new Control[] { name, notes, input, output };
            for (var i = 0; i < 4; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 1 ? 100 : 36));
                layout.Controls.Add(new Label { Text = labels[i], Dock = DockStyle.Fill }, 0, i); layout.Controls.Add(controls[i], 1, i);
            }
            var save = Ui.Button("Apply", delegate {
                if (String.IsNullOrWhiteSpace(name.Text)) return;
                item.Name = name.Text.Trim(); item.Notes = notes.Text;
                item.InputIndex = Math.Max(0, input.SelectedIndex); item.OutputIndex = Math.Max(0, output.SelectedIndex);
                DialogResult = DialogResult.OK;
            }, true);
            layout.Controls.Add(Ui.Bar(save), 1, 4); Controls.Add(layout); AcceptButton = save;
        }
    }
}

