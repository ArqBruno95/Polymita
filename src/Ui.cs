using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace WireShelf
{
    internal sealed class SearchBox : TextBox
    {
        internal string Hint = "Search…";
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, string lParam);
        protected override void OnHandleCreated(EventArgs e)
        { base.OnHandleCreated(e); SendMessage(Handle, 0x1501, new IntPtr(1), Hint); }
    }
    internal static class Ui
    {
        internal static readonly Color Ink = Color.FromArgb(39, 35, 30);
        internal static readonly Color Accent = Color.FromArgb(168, 53, 37);
        internal static readonly Color Muted = Color.FromArgb(110, 101, 87);
        internal static readonly Color Paper = Color.FromArgb(252, 248, 236);
        internal static void Style(Form form, string title, Size size)
        {
            form.Text = title; form.ClientSize = size; form.MinimumSize = size;
            form.Font = new Font("Segoe UI", 9F); form.ForeColor = Ink; form.BackColor = Paper;
            form.StartPosition = FormStartPosition.CenterParent; form.ShowInTaskbar = false;
            form.AutoScaleMode = AutoScaleMode.Dpi; form.MaximizeBox = false; form.MinimizeBox = false;
        }
        internal static Button Button(string text, Action click, bool primary = false)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 32, MinimumSize = new Size(72, 32),
                FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : Ink, Margin = new Padding(4), Padding = new Padding(6, 2, 6, 2) };
            b.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(214, 197, 158);
            b.Click += delegate { Safe(click); }; return b;
        }
        internal static FlowLayoutPanel Bar(params Control[] controls)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4), WrapContents = true };
            bar.Controls.AddRange(controls); return bar;
        }
        internal static void Safe(Action action)
        { try { action(); } catch (Exception ex) { Error(ex); } }
        internal static void Error(Exception ex)
        { MessageBox.Show(Grasshopper.Instances.DocumentEditor, ex.Message, "Polymita", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        internal static string Ask(IWin32Window owner, string title, string value, int maxLength)
        {
            using (var dialog = new Form())
            {
                Style(dialog, title, new Size(420, 125)); dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                var input = new TextBox { Text = value, MaxLength = maxLength, Dock = DockStyle.Top, Margin = new Padding(16) };
                var ok = Button("OK", delegate { if (!String.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = DialogResult.OK; }, true);
                var cancel = Button("Cancel", delegate { dialog.DialogResult = DialogResult.Cancel; });
                var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) }; host.Controls.Add(input);
                var bar = Bar(ok, cancel); bar.Dock = DockStyle.Bottom;
                dialog.Controls.Add(host); dialog.Controls.Add(bar); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                dialog.Shown += delegate { input.Focus(); input.SelectAll(); };
                return dialog.ShowDialog(owner) == DialogResult.OK ? input.Text.Trim() : null;
            }
        }
    }
}


