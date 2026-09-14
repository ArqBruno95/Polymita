using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WireShelf
{
    internal sealed class Palette : Form
    {
        private readonly GH_Canvas canvas;
        private readonly GH_Document document;
        private readonly IGH_Param anchor;
        private readonly bool fromInput;
        private readonly PointF position;
        private readonly IconGrid grid = new IconGrid();
        private readonly TextBox search = new SearchBox { Hint = "Search favorites…" };
        private readonly int columns, banks;
        internal Palette(GH_Canvas canvas, IGH_Param anchor, bool fromInput, PointF position, Point screen)
        {
            this.canvas = canvas; document = canvas.Document; this.anchor = anchor; this.fromInput = fromInput; this.position = position;
            var area = Screen.FromPoint(screen).WorkingArea;
            var sections = ShelfRuntime.Library.Search("").ToList();
            banks = area.Width >= 680 ? 2 : 1;
            var maxColumns = Math.Max(1, (area.Width - 64 - (banks - 1) * 16) / (ShelfGridLayout.Cell * banks));
            columns = Math.Min(9, maxColumns);
            ShelfGridLayout measurement;
            do {
                measurement = new ShelfGridLayout(sections, columns, banks);
                if (measurement.Size.Height <= area.Height - 132 || columns >= maxColumns) break;
                columns++;
            } while (true);
            Ui.Style(this, anchor == null ? "Polymita · No wire" : "Polymita · Connect", measurement.Size);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedToolWindow; KeyPreview = true; StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(measurement.Size.Width + 24, Math.Min(measurement.Size.Height + 78, area.Height - 44));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 8, 12, 8) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
            search.Dock = DockStyle.Fill; search.AccessibleName = "Search favorites by name, notes or section";
            search.TextChanged += delegate { Rebuild(); };
            var edit = Ui.Button("Edit library…", delegate { Close(); ShelfRuntime.Edit(); }); edit.Dock = DockStyle.Fill;
            edit.AutoSize = false; edit.MinimumSize = Size.Empty; edit.Margin = new Padding(4, 0, 0, 4); edit.Padding = Padding.Empty;
            toolbar.Controls.Add(search, 0, 0); toolbar.Controls.Add(edit, 1, 0);
            grid.Dock = DockStyle.Fill; grid.Margin = Padding.Empty;
            grid.ActivateItem += item => Ui.Safe(() => Insert(item, true, fromInput ? item.OutputIndex : item.InputIndex));
            grid.ContextItem += ShowContext;
            var hint = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ui.Muted,
                Text = anchor == null ? "Click: insert · Right-click: options · Esc: close" : "Click: connect · Right-click: choose port or insert without wire" };
            layout.Controls.Add(toolbar, 0, 0); layout.Controls.Add(grid, 0, 1); layout.Controls.Add(hint, 0, 2); Controls.Add(layout);
            Rebuild();
            Location = new Point(Math.Max(area.Left, Math.Min(screen.X, area.Right - Width)), Math.Max(area.Top, Math.Min(screen.Y, area.Bottom - Height)));
            Shown += delegate { Activate(); search.Focus(); };
            KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Escape) { Close(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { grid.InvokeSelected(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up || (grid.Focused && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))) {
                    grid.MoveSelection(e.KeyCode); e.SuppressKeyPress = true;
                }
            };
        }
        private void Rebuild() { grid.SetLayout(new ShelfGridLayout(ShelfRuntime.Library.Search(search.Text).ToList(), columns, banks)); }
        private void Insert(ShelfItem item, bool withWire, int port)
        {
            if (canvas.Document != document) throw new InvalidOperationException("The definition has changed. Open Polymita again.");
            if(item.IsAction) CanvasOperations.Run(document,item.ActionId);
            else Insertion.Insert(document, item, position, withWire ? anchor : null, fromInput, port);
            canvas.Refresh(); Close(); canvas.Focus();
        }
        private void ShowContext(ShelfItem item, Point point)
        {
            Ui.Safe(delegate {
                var menu = new ContextMenuStrip();
                menu.Items.Add(new ToolStripMenuItem(item.Name) { Enabled = false });
                if (anchor != null && !item.IsAction) {
                    var available = Recipes.Ports(Recipes.Create(item), fromInput);
                    for (var i = 0; i < available.Count; i++) {
                        var index = i;
                        menu.Items.Add((fromInput ? "Output " : "Input ") + (i + 1) + " · " + available[i].Name,
                            null, delegate { Ui.Safe(() => Insert(item, true, index)); });
                    }
                    if (available.Count == 0) menu.Items.Add(new ToolStripMenuItem("No port in this direction") { Enabled = false });
                    menu.Items.Add(new ToolStripSeparator());
                }
                menu.Items.Add(item.IsAction ? "Run operation" : "Insert without a wire", null, delegate { Ui.Safe(() => Insert(item, false, 0)); });
                menu.Items.Add("Edit library…", null, delegate { Close(); ShelfRuntime.Edit(); });
                // Closed can run before the item's Click handler. Dispose after
                // dispatch so choosing a port can finish inserting the component.
                menu.Closed += delegate { if (!IsDisposed) BeginInvoke(new Action(menu.Dispose)); else menu.Dispose(); }; menu.Show(point);
            });
        }
    }

    internal sealed class IconGrid : ScrollableControl
    {
        private ShelfGridLayout layout;
        private readonly Dictionary<Guid, Image> icons = new Dictionary<Guid, Image>();
        private readonly ToolTip tip = new ToolTip { InitialDelay = 350, ReshowDelay = 80, AutoPopDelay = 8000 };
        private int selected = -1, hovered = -1;
        internal event Action<ShelfItem> ActivateItem;
        internal event Action<ShelfItem, Point> ContextItem;
        internal IconGrid()
        {
            DoubleBuffered = true; AutoScroll = true; BackColor = Color.White; TabStop = true;
            AccessibleName = "Favorites grid. Use arrow keys to navigate and Enter to insert.";
            SetStyle(ControlStyles.Selectable, true);
        }
        internal void SetLayout(ShelfGridLayout value)
        { layout = value; selected = layout.Cells.Count > 0 ? 0 : -1; hovered = -1; tip.SetToolTip(this, ""); AutoScrollPosition = Point.Empty; AutoScrollMinSize = layout.Size; Invalidate(); }
        private int Hit(Point point)
        {
            if (layout == null) return -1;
            point.Offset(-AutoScrollPosition.X, -AutoScrollPosition.Y);
            return layout.Cells.FindIndex(c => c.Bounds.Contains(point));
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); var index = Hit(e.Location);
            if (index == hovered) return; hovered = index;
            tip.SetToolTip(this, index < 0 ? "" : layout.Cells[index].Item.Name + (layout.Cells[index].Item.IsRecipe ? " · Recipe" : "") + "\n" + layout.Cells[index].Item.Notes);
            Invalidate();
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = -1; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e); var index = Hit(e.Location); if (index < 0) return;
            selected = index; Focus(); Invalidate();
            if (e.Button == MouseButtons.Left) InvokeSelected();
            else if (e.Button == MouseButtons.Right && ContextItem != null) ContextItem(layout.Cells[index].Item, PointToScreen(e.Location));
        }
        internal void InvokeSelected() { if (layout != null && selected >= 0 && ActivateItem != null) ActivateItem(layout.Cells[selected].Item); }
        internal void MoveSelection(Keys direction)
        {
            if (layout == null || layout.Cells.Count == 0) return;
            if (selected < 0) selected = 0;
            var origin = layout.Cells[selected].Bounds.Location;
            var best = double.MaxValue; var next = selected;
            for (var i = 0; i < layout.Cells.Count; i++) {
                var target = layout.Cells[i].Bounds.Location; var dx = target.X - origin.X; var dy = target.Y - origin.Y;
                var vertical = direction == Keys.Up || direction == Keys.Down;
                var forward = direction == Keys.Up ? -dy : direction == Keys.Down ? dy : direction == Keys.Left ? -dx : dx;
                if (forward <= 0) continue;
                var cross = vertical ? dx : dy; var score = forward + Math.Abs(cross) * 4.0;
                if (score < best) { best = score; next = i; }
            }
            selected = next; Focus(); AccessibleDescription = layout.Cells[selected].Item.Name;
            var bounds = layout.Cells[selected].Bounds; var top = -AutoScrollPosition.Y;
            if (bounds.Top < top) AutoScrollPosition = new Point(0, bounds.Top);
            else if (bounds.Bottom > top + ClientSize.Height) AutoScrollPosition = new Point(0, bounds.Bottom - ClientSize.Height);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); if (layout == null) return;
            e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            using (var font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold))
                foreach (var heading in layout.Headings) {
                    using (var brush = new SolidBrush(Ui.Paper)) e.Graphics.FillRectangle(brush, heading.Bounds);
                    var text = heading.Bounds; text.Inflate(-5, 0);
                    TextRenderer.DrawText(e.Graphics, heading.Title, font, text, Ui.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsTranslateTransform);
                }
            for (var i = 0; i < layout.Cells.Count; i++) {
                var cell = layout.Cells[i]; var bounds = cell.Bounds; bounds.Inflate(-2, -2);
                if (i == hovered || i == selected) {
                    using (var brush = new SolidBrush(Color.FromArgb(252, 230, 155))) e.Graphics.FillRectangle(brush, bounds);
                    using (var pen = new Pen(Ui.Accent)) e.Graphics.DrawRectangle(pen, bounds);
                }
                Image icon;
                if(cell.Item.IsAction) icon=cell.Item.ActionId=="connect"?Brand.Connect:Brand.Duplicate;
                else if (!icons.TryGetValue(cell.Item.ComponentId, out icon)) {
                    var proxy = Instances.ComponentServer.EmitObjectProxy(cell.Item.ComponentId);
                    icon = proxy == null ? null : proxy.Icon; icons[cell.Item.ComponentId] = icon;
                }
                if (icon != null) e.Graphics.DrawImage(icon, cell.Bounds.X + 6, cell.Bounds.Y + 6, 24, 24);
                else TextRenderer.DrawText(e.Graphics, "?", Font, bounds, Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsTranslateTransform);
                if (cell.Item.IsRecipe) using (var brush = new SolidBrush(Ui.Accent)) e.Graphics.FillEllipse(brush, bounds.Right - 5, bounds.Bottom - 5, 5, 5);
            }
            if (layout.Cells.Count == 0) TextRenderer.DrawText(e.Graphics, "No matching favorites.", Font, new Point(8, 12), Ui.Muted);
        }
        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
    }
}


