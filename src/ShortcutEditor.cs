using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Polymita
{
    internal sealed class ShortcutEditor : Form
    {
        private readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details,
            FullRowSelect = true, MultiSelect = false, HideSelection = false,
            AccessibleName = "Commands and the keys they are bound to" };
        private readonly Dictionary<string,Keys> draft = new Dictionary<string,Keys>();
        internal ShortcutEditor()
        {
            Ui.Style(this, "Polymita · Shortcuts", new Size(500, 460));
            foreach (var entry in Commands.All) draft[entry.Id] = Commands.Key(entry.Id);
            list.Columns.Add("Command", 285); list.Columns.Add("Shortcut", 165);
            list.KeyDown += Bind;
            var hint = new Label { Dock = DockStyle.Top, Height = 44, ForeColor = Ui.Muted, Text =
                "Select a command and press the keys you want. A shortcut needs at least one of\n" +
                "Ctrl, Shift or Alt. Backspace clears it. Taking a combination frees its old owner." };
            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            host.Controls.Add(list); host.Controls.Add(hint);
            var bar = Ui.Bar(Ui.Button("Apply", Apply, true), Ui.Button("Restore defaults", Restore), Ui.Button("Close", Close));
            bar.Dock = DockStyle.Bottom;
            Controls.Add(host); Controls.Add(bar);
            Fill(null);
            Shown += delegate { if (list.Items.Count > 0) { list.Items[0].Selected = true; list.Focus(); } };
        }
        private void Fill(string select)
        {
            list.BeginUpdate(); list.Items.Clear();
            foreach (var entry in Commands.All)
            {
                var row = new ListViewItem(new[] { entry.Title, Commands.Describe(draft[entry.Id]) }) { Tag = entry.Id };
                list.Items.Add(row);
                if (entry.Id == select) { row.Selected = true; row.EnsureVisible(); }
            }
            list.EndUpdate();
        }
        private void Bind(object sender, KeyEventArgs e)
        {
            if (list.SelectedItems.Count != 1) return;
            var id = (string)list.SelectedItems[0].Tag;
            // The list must never act on the keystroke itself: every key here is a binding.
            e.SuppressKeyPress = true; e.Handled = true;
            if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete) { draft[id] = Keys.None; Fill(id); return; }
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu) return;
            if (e.KeyCode == Keys.Tab || e.KeyCode == Keys.Escape) return;
            // Without a modifier the binding would swallow ordinary typing on the canvas.
            if (e.Modifiers == Keys.None) return;
            var combo = e.KeyCode | e.Modifiers;
            foreach (var other in Commands.All.Where(x => x.Id != id).ToArray())
                if (draft[other.Id] == combo) draft[other.Id] = Keys.None;
            draft[id] = combo; Fill(id);
        }
        private void Restore()
        {
            foreach (var entry in Commands.All) draft[entry.Id] = entry.Fallback;
            Fill(null);
        }
        private void Apply()
        {
            foreach (var entry in Commands.All) Commands.Bind(entry.Id, draft[entry.Id]);
            ShelfRuntime.ShortcutsChanged();
            Text = "Polymita · Shortcuts saved";
        }
    }
}
