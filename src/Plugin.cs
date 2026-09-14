using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Polymita")]
[assembly: AssemblyDescription("Favorites, recipes and canvas tools for Grasshopper")]
[assembly: AssemblyVersion("0.7.0.0")]
[assembly: AssemblyFileVersion("0.7.0.0")]
[assembly: AssemblyCopyright("WireShelf contributors, 2026. GPL-3.0-or-later.")]

namespace WireShelf
{
    public sealed class WireShelfInfo : GH_AssemblyInfo
    {
        public override string Name { get { return "Polymita"; } }
        public override string Description { get { return "Your favorites, recipes and tools, one wire away."; } }
        public override Guid Id { get { return new Guid("0b6a3316-b9de-4441-8c44-2b893cf33731"); } }
        public override string AuthorName { get { return "Polymita"; } }
        public override string AuthorContact { get { return ""; } }
        public override string Version { get { return "0.7.0"; } }
        public override Bitmap Icon { get { return ShelfRuntime.Icon; } }
    }
    public sealed class Priority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        { ShelfRuntime.Initialize(); return GH_LoadingInstruction.Proceed; }
    }

    public static class ShelfRuntime
    {
        private static bool initialized;
        private static Timer startup;
        private static readonly HashSet<GH_Canvas> canvases = new HashSet<GH_Canvas>();
        private static LibraryStore store;
        private static bool loadFailed;
        private static ShelfLibrary library;
        private static Editor editor;
        private static Palette palette;
        private static ToolStripMenuItem menu;
        private static ShiftFilter shiftFilter;
        private static Toolbox toolbox;
        private static ToolboxSettings toolboxSettings;
        private static string ToolboxPath { get { return Path.Combine(Folders.SettingsFolder, "WireShelf", "toolbox.json"); } }
        public static bool Enabled = true;
        public static readonly Bitmap Icon = Brand.Main;
        private static readonly Dictionary<GH_Canvas,CanvasLabels> labels = new Dictionary<GH_Canvas,CanvasLabels>();
        private static readonly Dictionary<GH_Canvas,CanvasGestures> gestures = new Dictionary<GH_Canvas,CanvasGestures>();
        private static readonly List<ToolStripButton> buttons = new List<ToolStripButton>();
        public static ShelfLibrary Library
        {
            get
            {
                if (library == null)
                {
                    store = new LibraryStore(Path.Combine(Folders.SettingsFolder, "WireShelf", "library.wireshelf.json"));
                    try
                    {
                        library = store.Load() ?? Recipes.Defaults();
                        // Freshly read from disk or freshly built, so the migrations can run
                        // on it directly; a failure is discarded by the catch below anyway.
                        var expanded = library;
                        var changed=BuiltInCatalog.Apply(expanded);
                        if(expanded.OperationsRevision<1) {
                            expanded.Sections.Add(new ShelfSection { Title="Operations",Items=new List<ShelfItem> {
                                new ShelfItem { ActionId="connect",Name="Connect selection · Alt+W",Notes="Connects left to right, in port order." },
                                new ShelfItem { ActionId="duplicate",Name="Duplicate selection · Alt+Q",Notes="Copies components and complete groups to free space on the right." }
                            } }); expanded.OperationsRevision=1; changed=true;
                        }
                        changed=EnglishLibrary.Apply(expanded) || changed;
                        if (changed) { store.Save(expanded); library = expanded; }
                        if (store.Recovered) MessageBox.Show(Instances.DocumentEditor,
                            "The main library is damaged. A backup was recovered. Save from the editor to repair it.", "Polymita");
                    }
                    catch (Exception ex)
                    {
                        loadFailed = true; library = new ShelfLibrary();
                        Ui.Error(new IOException("The library could not be read. The original file is preserved.\n" + ex.Message));
                    }
                }
                return library;
            }
        }
        public static void Save(ShelfLibrary updated)
        {
            var current = Library; // Initialize the store before use.
            if (loadFailed && File.Exists(store.FilePath))
                File.Copy(store.FilePath, store.FilePath + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true);
            store.Save(updated); library = LibraryStore.Copy(updated); loadFailed = false;
        }
        public static void Initialize()
        {
            if (initialized) return; initialized = true; Enabled = true;
            try { toolboxSettings = ToolboxSettings.Load(ToolboxPath); } catch (Exception ex) { toolboxSettings = new ToolboxSettings(); Ui.Error(ex); }
            WireStyles.Variant=toolboxSettings.WireVariant;
            CanvasGestures.CutEnabled=toolboxSettings.CutWires; CanvasGestures.SnapEnabled=toolboxSettings.SnapAlign;
            Ui.Safe(()=>WireStyles.SetPolylines(toolboxSettings.Polylines));
            WireStyles.SetHighlight(toolboxSettings.Highlight,Color.FromArgb(toolboxSettings.SelectedArgb));
            shiftFilter = new ShiftFilter();
            Instances.CanvasCreated += Attach;
            Instances.CanvasDestroyed += Detach;
            if (Instances.ActiveCanvas != null) Attach(Instances.ActiveCanvas);
            startup = new Timer { Interval = 350 };
            startup.Tick += delegate {
                if (Instances.DocumentEditor == null || Instances.ActiveCanvas == null) return;
                Attach(Instances.ActiveCanvas);
                if (InstallMenu()) { startup.Stop(); startup.Dispose(); startup = null; }
            };
            startup.Start();
        }
        public static void Shutdown()
        {
            if (toolbox != null) { toolbox.Dispose(); toolbox = null; }
            WireStyles.Reset();
            Enabled = false;
            Instances.CanvasCreated -= Attach; Instances.CanvasDestroyed -= Detach;
            if (shiftFilter != null) { shiftFilter.Dispose(); shiftFilter = null; }
            foreach (var canvas in canvases.ToArray()) Detach(canvas);
            if (palette != null) palette.Close();
            if (editor != null) editor.Close();
            if (menu != null) menu.Dispose();
            foreach(var button in buttons) button.Dispose(); buttons.Clear();
            if (startup != null) { startup.Stop(); startup.Dispose(); startup = null; }
            initialized = false;
        }
        private static void Attach(GH_Canvas canvas)
        {
            if (!canvases.Add(canvas)) return;
            canvas.MouseDown += MouseDown; canvas.KeyDown += KeyDown;
            labels.Add(canvas,new CanvasLabels(canvas,toolboxSettings));
            gestures.Add(canvas,new CanvasGestures(canvas));
            canvas.Disposed += delegate { Detach(canvas); };
        }
        private static void Detach(GH_Canvas canvas)
        {
            if (!canvases.Remove(canvas)) return;
            canvas.MouseDown -= MouseDown; canvas.KeyDown -= KeyDown;
            CanvasGestures gesture; if(gestures.TryGetValue(canvas,out gesture)) { gesture.Dispose(); gestures.Remove(canvas); }
            CanvasLabels overlay; if(labels.TryGetValue(canvas,out overlay)) { overlay.Dispose(); labels.Remove(canvas); }
        }
        private static bool InstallMenu()
        {
            var host = Instances.DocumentEditor;
            var strip = host.MainMenuStrip ?? host.Controls.OfType<MenuStrip>().FirstOrDefault();
            if (strip == null) return false;
            if (menu != null && !menu.IsDisposed) return true;
            menu = new ToolStripMenuItem("Polymita", Icon);
            var active = new ToolStripMenuItem("Enable on wire release") { Checked = Enabled, CheckOnClick = true };
            active.CheckedChanged += delegate { Enabled = active.Checked; };
            menu.DropDownItems.Add(active);
            var cut = new ToolStripMenuItem("Cut wires · Ctrl + left drag") { Checked=CanvasGestures.CutEnabled, CheckOnClick=true };
            cut.CheckedChanged += delegate {
                CanvasGestures.CutEnabled=cut.Checked; toolboxSettings.CutWires=cut.Checked; SaveToolboxSettings();
            }; menu.DropDownItems.Add(cut);
            var snap = new ToolStripMenuItem("Snap to edges and centers · hold Alt to suspend") { Checked=CanvasGestures.SnapEnabled, CheckOnClick=true };
            snap.CheckedChanged += delegate {
                CanvasGestures.SnapEnabled=snap.Checked; toolboxSettings.SnapAlign=snap.Checked; SaveToolboxSettings();
            }; menu.DropDownItems.Add(snap);
            menu.DropDownItems.Add(new ToolStripMenuItem("Edit library…", null, delegate { Edit(); })
                { ShortcutKeys = Keys.Control | Keys.Shift | Keys.B });
            menu.DropDownItems.Add(new ToolStripMenuItem("Save selection as recipe…", null, delegate { Ui.Safe(() => Edit(CaptureSelection())); })
                { ShortcutKeys = Keys.Control | Keys.Shift | Keys.R });
            menu.DropDownItems.Add(new ToolStripMenuItem("Open favorites here", null, delegate { Ui.Safe(OpenAtCenter); })
                { ShortcutKeys = Keys.Control | Keys.Space });
            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add("Rhino viewport", null, delegate { OpenToolbox(0); });
            menu.DropDownItems.Add(new ToolStripMenuItem("Find in definition / Profiler", null, delegate { OpenToolbox(1); }) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.F });
            menu.DropDownItems.Add("Wire style and color", null, delegate { OpenToolbox(2); });
            menu.DropDownItems.Add("Component and group labels",Brand.Labels,delegate { OpenToolbox(3); });
            menu.DropDownItems.Add(new ToolStripMenuItem("Connect selection",Brand.Connect,delegate { RunOperation("connect"); }) { ShortcutKeyDisplayString="Alt+W" });
            menu.DropDownItems.Add(new ToolStripMenuItem("Duplicate selection",Brand.Duplicate,delegate { RunOperation("duplicate"); }) { ShortcutKeyDisplayString="Alt+Q" });
            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add("About / Help", null, delegate {
                MessageBox.Show(host, "Polymita 0.7.0 · Rhino 8 / Windows\n\n" +
                    "Drag a wire from an input or output and release over empty canvas. You can also click a port and then empty canvas.\n\n" +
                    "Double Shift: insert without a wire at the cursor. Click an icon to insert; right-click to choose a port.\n\n" +
                    "Ctrl+Space: favorites · Ctrl+Shift+B: library · Ctrl+Shift+R: capture selection.\n\n" +
                    "Configure a component, select it and save it as a recipe. Its settings and persistent data are preserved.\n\n" +
                    "Disable QuickConnection if both plugins intercept the same wires.\n\n" +
                    "Alt+W: connect selection · Alt+Q: duplicate selection.\nCtrl+Shift+F: find / Profiler.\n\n" +
                    "Four buttons beside Sketch: viewport, library, wires and labels.\n\n" +
                    "Inspired by QuickConnection, WiresRenderer and Sunglasses.\nGPL-3.0-or-later · License and source included in the package.", "Polymita");
            });
            strip.Items.Add(menu); InstallToolbar(host); return true;
        }
        private static IEnumerable<Control> Descendants(Control root)
        { foreach(Control child in root.Controls) { yield return child; foreach(var nested in Descendants(child)) yield return nested; } }
        private static void InstallToolbar(Control host)
        {
            if(buttons.Count>0) return;
            foreach(var strip in Descendants(host).OfType<ToolStrip>().Where(s=>!(s is MenuStrip)))
            {
                var pencil=strip.Items.Cast<ToolStripItem>().FirstOrDefault(i=>(i.Name+" "+i.ToolTipText+" "+i.Text).IndexOf("sketch",StringComparison.OrdinalIgnoreCase)>=0);
                if(pencil==null) continue;
                var icons=new[] { Brand.View,Brand.Library,Brand.Wires,Brand.Labels };
                var titles=new[] { "Polymita · Show / hide viewport", "Polymita · Edit library", "Polymita · Wire style", "Polymita · Component and group labels" };
                int index=strip.Items.IndexOf(pencil)+1;
                for(int i=0;i<4;i++) {
                    int action=i; var button=new ToolStripButton { Name="PolymitaTool"+i,Image=icons[i],DisplayStyle=ToolStripItemDisplayStyle.Image,ToolTipText=titles[i],AccessibleName=titles[i],AutoSize=false,Size=pencil.Size };
                    button.Click+=delegate { if(action==1) Edit(); else OpenToolbox(action==0?0:action); };
                    strip.Items.Insert(index++,button); buttons.Add(button);
                }
                break;
            }
        }
        internal static void SaveToolboxSettings()
        { if (toolboxSettings != null) Ui.Safe(delegate { toolboxSettings.Save(ToolboxPath); }); }
        internal static void RefreshToolbar()
        { if(buttons.Count>0 && !buttons[0].IsDisposed) buttons[0].Checked=toolbox!=null && toolbox.ViewportVisible; }
        internal static void RunOperation(string action)
        { Ui.Safe(delegate { var canvas=Instances.ActiveCanvas; CanvasOperations.Run(canvas==null?null:canvas.Document,action); if(canvas!=null) canvas.Invalidate(); }); }
        internal static void OpenToolbox(int tab)
        {
            Ui.Safe(delegate {
                var canvas = Instances.ActiveCanvas;
                if (canvas == null) return;
                if (toolboxSettings == null) toolboxSettings = ToolboxSettings.Load(ToolboxPath);
                if (toolbox == null)
                {
                    WireStyles.SetHighlight(toolboxSettings.Highlight, Color.FromArgb(toolboxSettings.SelectedArgb));
                    try { WireStyles.SetPolylines(toolboxSettings.Polylines); } catch (Exception ex) { Ui.Error(ex); }
                    toolbox = new Toolbox(canvas, toolboxSettings, ToolboxPath);
                    canvas.Disposed += delegate { if (toolbox != null) { toolbox.Dispose(); toolbox = null; } };
                }
                toolbox.SelectTab(tab);
            });
        }
        private static void KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.B) { Edit(); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.R) { Ui.Safe(() => Edit(CaptureSelection())); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Space) { Ui.Safe(OpenAtCenter); e.Handled = true; e.SuppressKeyPress = true; }
        }
        private static void OpenAtCenter()
        {
            var canvas = Instances.ActiveCanvas;
            if (canvas == null || canvas.Document == null) throw new InvalidOperationException("Open a definition first.");
            var screen = canvas.PointToScreen(new Point(canvas.Width / 2, canvas.Height / 2));
            var local = canvas.PointToClient(screen);
            var ev = new GH_CanvasMouseEvent(canvas.Viewport, new MouseEventArgs(MouseButtons.None, 0, local.X, local.Y, 0));
            ShowPalette(canvas, null, false, ev.CanvasLocation, screen);
        }
        internal static void OpenAtCursor(GH_Canvas canvas)
        {
            var screen = Cursor.Position; var local = canvas.PointToClient(screen);
            if (!canvas.ClientRectangle.Contains(local) || canvas.Document == null) return;
            var ev = new GH_CanvasMouseEvent(canvas.Viewport, new MouseEventArgs(MouseButtons.None, 0, local.X, local.Y, 0));
            ShowPalette(canvas, null, false, ev.CanvasLocation, screen);
        }
        public static ShelfItem CaptureSelection()
        {
            var canvas = Instances.ActiveCanvas;
            if (canvas == null || canvas.Document == null) throw new InvalidOperationException("Open a definition and select a component.");
            var selection = canvas.Document.Objects.Where(o => o.Attributes.Selected && (o is IGH_Component || o is IGH_Param)).ToList();
            if (selection.Count != 1) throw new InvalidOperationException("Select exactly one component or parameter to save its configuration.");
            return Recipes.Capture(selection[0]);
        }
        internal static void Edit(ShelfItem captured = null)
        {
            Ui.Safe(delegate {
                if (editor != null && !editor.IsDisposed)
                {
                    editor.Activate();
                    if (captured != null) MessageBox.Show(editor, "Use Capture selection in the open editor.", "Polymita");
                    return;
                }
                editor = new Editor(captured);
                // CenterParent does not reliably position modeless windows in Rhino's host.
                var area = Screen.FromControl(Instances.DocumentEditor).WorkingArea;
                editor.StartPosition = FormStartPosition.Manual;
                editor.Location = new Point(area.Left + Math.Max(0, (area.Width - editor.Width) / 2),
                    area.Top + Math.Max(0, (area.Height - editor.Height) / 2));
                editor.Show(Instances.DocumentEditor); editor.Activate();
            });
        }
        internal static void ShowPalette(GH_Canvas canvas, IGH_Param anchor, bool fromInput, PointF position, Point screen)
        {
            Ui.Safe(delegate {
                if (palette != null && !palette.IsDisposed) palette.Close();
                palette = new Palette(canvas, anchor, fromInput, position, screen); palette.Show(Instances.DocumentEditor);
            });
        }
        private static void MouseDown(object sender, MouseEventArgs e)
        {
            if (shiftFilter != null) shiftFilter.Cancel();
            var canvas = (GH_Canvas)sender;
            if (!Enabled || e.Button != MouseButtons.Left || ModifierKeysHeld() || canvas.Document == null) return;
            // Do not take over rewiring or another extension's interaction.
            var interaction = canvas.ActiveInteraction;
            if (interaction == null || interaction.GetType() != typeof(GH_WireInteraction)) return;
            var ev = new GH_CanvasMouseEvent(canvas.Viewport, e);
            var attr = canvas.Document.FindAttributeByGrip(ev.CanvasLocation, false, true, true, 10);
            var source = attr == null ? null : attr.DocObject as IGH_Param;
            if (source == null) return;
            var fromInput = attr.HasInputGrip && (!attr.HasOutputGrip || Distance(ev.CanvasLocation, attr.InputGrip) <= Distance(ev.CanvasLocation, attr.OutputGrip));
            canvas.ActiveInteraction = new ShelfWireInteraction(canvas, ev, source, fromInput);
        }
        private static bool ModifierKeysHeld() { return Control.ModifierKeys != Keys.None; }
        internal static float Distance(PointF a, PointF b)
        { var x = a.X - b.X; var y = a.Y - b.Y; return (float)Math.Sqrt(x * x + y * y); }
    }

    // The interaction extension point follows QuickConnection. Native wire movement and
    // connection are delegated to GH_WireInteraction; no private SDK fields are inspected.
    internal sealed class ShelfWireInteraction : GH_WireInteraction
    {
        private readonly IGH_Param source;
        private readonly bool fromInput;
        private readonly Point origin;
        private readonly GH_Document document;
        private bool firstRelease = true;
        internal ShelfWireInteraction(GH_Canvas canvas, GH_CanvasMouseEvent e, IGH_Param source, bool fromInput) : base(canvas, e, source)
        { this.source = source; this.fromInput = fromInput; origin = e.ControlLocation; document = canvas.Document; }
        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Right) return GH_ObjectResponse.Release;
            return e.Button == MouseButtons.Left ? RespondToMouseUp(sender, e) : base.RespondToMouseDown(sender, e);
        }
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (sender.Document != document) return GH_ObjectResponse.Release;
            if (e.Button != MouseButtons.Left) return base.RespondToMouseUp(sender, e);
            var target = document.FindAttributeByGrip(e.CanvasLocation, false, !fromInput, fromInput, 10);
            if (target != null || Control.ModifierKeys != Keys.None) return base.RespondToMouseUp(sender, e);
            var isClick = ShelfRuntime.Distance(origin, e.ControlLocation) < 6;
            if (firstRelease && isClick) { firstRelease = false; return GH_ObjectResponse.Ignore; }
            firstRelease = false;
            if (document.FindAttribute(e.CanvasLocation, true) != null) return base.RespondToMouseUp(sender, e);
            var screen = sender.PointToScreen(e.ControlLocation); var position = e.CanvasLocation;
            sender.BeginInvoke(new Action(delegate {
                if (!sender.IsDisposed && sender.Document == document) ShelfRuntime.ShowPalette(sender, source, fromInput, position, screen);
            }));
            return GH_ObjectResponse.Release;
        }
    }
}



