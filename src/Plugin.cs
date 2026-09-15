using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Polymita")]
[assembly: AssemblyDescription("Favorites, recipes and canvas tools for Grasshopper")]
[assembly: AssemblyVersion("0.8.7.0")]
[assembly: AssemblyFileVersion("0.8.7.0")]
[assembly: AssemblyCopyright("Polymita contributors, 2026. GPL-3.0-or-later.")]

namespace Polymita
{
    public sealed class PolymitaInfo : GH_AssemblyInfo
    {
        public override string Name { get { return "Polymita"; } }
        public override string Description { get { return "Your favorites, recipes and tools, one wire away."; } }
        public override Guid Id { get { return new Guid("0b6a3316-b9de-4441-8c44-2b893cf33731"); } }
        public override string AuthorName { get { return "Polymita"; } }
        public override string AuthorContact { get { return ""; } }
        public override string Version { get { return "0.8.7"; } }
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
        private static ShortcutEditor shortcuts;
        private static ShiftFilter shiftFilter;
        private static Toolbox toolbox;
        private static ToolboxSettings toolboxSettings;
        private static SettingsStorage storage;
        internal static SettingsStorage Storage
        {
            get
            {
                if (storage == null) storage = new SettingsStorage(Folders.SettingsFolder);
                storage.Prepare();
                return storage;
            }
        }
        private static string ToolboxPath { get { return Storage.ToolboxPath; } }
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
                    store = new LibraryStore(Storage.LibraryPath);
                    try
                    {
                        library = store.Load() ?? Recipes.Defaults();
                        // Freshly read from disk or freshly built, so the migrations can run
                        // on it directly; a failure is discarded by the catch below anyway.
                        var expanded = library;
                        var changed=BuiltInCatalog.Apply(expanded);
                        // Operations remain available from the menu and shortcuts.
                        // Existing user-added actions are retained as text rows.
                        if (expanded.OperationsRevision < 2) { expanded.OperationsRevision = 2; changed = true; }
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
            Commands.Load(toolboxSettings.Shortcuts);
            // Captions and segmented wires became defaults rather than opt-ins. Settings
            // files written before that carry an explicit false, so turn them on once and
            // leave them under the user's control from then on.
            if (toolboxSettings.LabelsRevision < 1)
            { toolboxSettings.ComponentNames = true; toolboxSettings.LabelsRevision = 1; SaveToolboxSettings(); }
            if (toolboxSettings.WiresRevision < 1)
            { toolboxSettings.Polylines = true; toolboxSettings.WireVariant = 1; toolboxSettings.WiresRevision = 1; SaveToolboxSettings(); }
            WireStyles.Variant=toolboxSettings.WireVariant;
            CanvasGestures.CutEnabled=toolboxSettings.CutWires; CanvasGestures.SnapEnabled=toolboxSettings.SnapAlign;
            // Reported by the Wires tab when the option is next touched rather than as a
            // dialog on every start, which is what turning this on by default would mean.
            try { WireStyles.SetPolylines(toolboxSettings.Polylines); } catch (Exception ex) { WireStyles.LastFailure = ex.Message; }
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
            items.Clear();
            // Tools that have a toolbar icon come first, in toolbar order, then the
            // switches, then everything else. No separators: one flat list.
            Tool("viewport", Brand.View, delegate { OpenToolbox(0); });
            Tool("library", Brand.Library, delegate { Edit(); });
            Tool("wires", Brand.Wires, delegate { OpenToolbox(2); });
            Tool("labels", Brand.Labels, delegate { OpenToolbox(3); });
            Tool("finder", Brand.Find, delegate { OpenToolbox(1); });
            var active = new ToolStripMenuItem("Enable on wire release") { Checked = Enabled, CheckOnClick = true };
            active.CheckedChanged += delegate { Enabled = active.Checked; };
            menu.DropDownItems.Add(active);
            var cut = new ToolStripMenuItem("Cut wires · Ctrl + left drag") { Checked=CanvasGestures.CutEnabled, CheckOnClick=true };
            cut.CheckedChanged += delegate {
                CanvasGestures.CutEnabled=cut.Checked; toolboxSettings.CutWires=cut.Checked; SaveToolboxSettings();
            }; menu.DropDownItems.Add(cut);
            var snap = new ToolStripMenuItem("Snap to edges and centers") { Checked=CanvasGestures.SnapEnabled, CheckOnClick=true };
            snap.CheckedChanged += delegate {
                CanvasGestures.SnapEnabled=snap.Checked; toolboxSettings.SnapAlign=snap.Checked; SaveToolboxSettings();
            }; menu.DropDownItems.Add(snap);
            Tool("capture", null, delegate { Ui.Safe(() => Edit(CaptureSelection())); });
            Tool("palette", null, delegate { Ui.Safe(OpenAtCenter); });
            Tool("connect", null, delegate { RunOperation("connect"); });
            Tool("duplicate", null, delegate { RunOperation("duplicate"); });
            Tool("before", null, delegate { RunOperation("before"); });
            Tool("after", null, delegate { RunOperation("after"); });
            Tool("absorb", null, delegate { RunOperation("absorb"); });
            menu.DropDownItems.Add("Customise shortcuts…", null, delegate { EditShortcuts(); });
            menu.DropDownItems.Add("About / Help", null, delegate {
                MessageBox.Show(host, "Polymita 0.8.7 · Rhino 8 / Windows\n\n" +
                    "Drag a wire from an input or output and release over empty canvas to pick a favorite.\n\n" +
                    "The Rhino viewport carries Rhino's command line and its options, the object snaps, the status toggles and a distance readout.\n\n" +
                    "Double Shift: insert without a wire at the cursor. Click an icon to insert; right-click to choose a port.\n\n" +
                    "Ctrl + left drag cuts wires. Shift while dragging constrains the move.\n\n" +
                    "Every shortcut can be rebound from Customise shortcuts.\n\n" +
                    "Inspired by QuickConnection, WiresRenderer and Sunglasses.\nGPL-3.0-or-later · License and source included in the package.", "Polymita");
            });
            strip.Items.Add(menu); InstallToolbar(host); return true;
        }
        private static readonly Dictionary<string,ToolStripMenuItem> items = new Dictionary<string,ToolStripMenuItem>();
        // A menu entry bound to a command, so rebinding updates it in place.
        private static void Tool(string id, Image icon, EventHandler click)
        {
            var entry = new ToolStripMenuItem(Commands.All.First(e => e.Id == id).Title, icon, click);
            items[id] = entry; menu.DropDownItems.Add(entry); ApplyKey(id, entry);
        }
        // Alt combinations are shown but not claimed: Grasshopper's canvas never hands
        // WinForms those keystrokes, so the Rhino-side hook is what actually runs them.
        private static void ApplyKey(string id, ToolStripMenuItem entry)
        {
            var combo = Commands.Key(id);
            entry.ShortcutKeys = Keys.None; entry.ShortcutKeyDisplayString = null;
            if (combo == Keys.None) return;
            if ((combo & Keys.Alt) != Keys.None) { entry.ShortcutKeyDisplayString = Commands.Describe(combo); return; }
            try { entry.ShortcutKeys = combo; }
            catch (ArgumentException) { entry.ShortcutKeyDisplayString = Commands.Describe(combo); }
        }
        internal static void ShortcutsChanged()
        {
            Ui.Safe(delegate {
                foreach (var pair in items) if (!pair.Value.IsDisposed) ApplyKey(pair.Key, pair.Value);
                if (toolboxSettings != null) { toolboxSettings.Shortcuts = Commands.Save(); SaveToolboxSettings(); }
            });
        }
        internal static void EditShortcuts()
        {
            Ui.Safe(delegate {
                if (shortcuts != null && !shortcuts.IsDisposed) { shortcuts.Activate(); return; }
                shortcuts = new ShortcutEditor();
                shortcuts.FormClosed += delegate { shortcuts = null; };
                shortcuts.Show(Instances.DocumentEditor);
            });
        }
        // Runs any command in the table, whichever key or menu entry reached it.
        internal static void RunCommand(string id)
        {
            switch (id)
            {
                case "viewport": OpenToolbox(0); break;
                case "library": Edit(); break;
                case "wires": OpenToolbox(2); break;
                case "labels": OpenToolbox(3); break;
                case "finder": OpenToolbox(1); break;
                case "capture": Ui.Safe(() => Edit(CaptureSelection())); break;
                case "palette": Ui.Safe(OpenAtCenter); break;
                default: RunOperation(id); break;
            }
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
                var icons=new[] { Brand.View,Brand.Library,Brand.Wires,Brand.Labels,Brand.Find };
                var titles=new[] { "Polymita · Show / hide viewport", "Polymita · Edit library", "Polymita · Wire style",
                    "Polymita · Component and group labels", "Polymita · Find in definition / Profiler · Ctrl+Shift+F" };
                // Toolbox tab each button opens; -1 is the library editor, which is its own window.
                var tabs=new[] { 0,-1,2,3,1 };
                int index=strip.Items.IndexOf(pencil)+1;
                for(int i=0;i<icons.Length;i++) {
                    int tab=tabs[i]; var button=new ToolStripButton { Name="PolymitaTool"+i,Image=icons[i],DisplayStyle=ToolStripItemDisplayStyle.Image,ToolTipText=titles[i],AccessibleName=titles[i],AutoSize=false,Size=pencil.Size };
                    button.Click+=delegate { if(tab<0) Edit(); else OpenToolbox(tab); };
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
                    try { WireStyles.SetPolylines(toolboxSettings.Polylines); } catch (Exception ex) { Ui.Error(ex); }
                    toolbox = new Toolbox(canvas, toolboxSettings, ToolboxPath);
                    canvas.Disposed += delegate { if (toolbox != null) { toolbox.Dispose(); toolbox = null; } };
                }
                toolbox.SelectTab(tab);
            });
        }
        private static void KeyDown(object sender, KeyEventArgs e)
        {
            var canvas=sender as GH_Canvas;
            if (canvas==null || canvas.ActiveInteraction!=null || Control.MouseButtons!=MouseButtons.None) return;
            // Alt combinations arrive through the Rhino-side hook instead.
            if ((e.Modifiers & Keys.Alt) != Keys.None) return;
            var id = Commands.Match(e.KeyCode | e.Modifiers);
            if (id == null) return;
            RunCommand(id); e.Handled = true; e.SuppressKeyPress = true;
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
            if (!Enabled || e.Button != MouseButtons.Left || e.Clicks > 1 || ModifierKeysHeld() || canvas.Document == null) return;
            // Do not take over rewiring or another extension's interaction.
            var interaction = canvas.ActiveInteraction;
            if (interaction == null || interaction.GetType() != typeof(GH_WireInteraction)) return;
            var ev = new GH_CanvasMouseEvent(canvas.Viewport, e);
            var attr = canvas.Document.FindAttributeByGrip(ev.CanvasLocation, false, true, true, ShelfWireInteraction.GripReach);
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
        // Grasshopper's own grip radius, in canvas units, for both the search that
        // names the wire's source and the one that rules the palette out on release.
        internal const int GripReach = 10;
        private readonly IGH_Param source;
        private readonly bool fromInput;
        private readonly Point origin;
        private readonly GH_Document document;
        internal ShelfWireInteraction(GH_Canvas canvas, GH_CanvasMouseEvent e, IGH_Param source, bool fromInput) : base(canvas, e, source)
        { this.source = source; this.fromInput = fromInput; origin = e.ControlLocation; document = canvas.Document; }
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (sender.Document != document) return GH_ObjectResponse.Release;
            if (e.Button != MouseButtons.Left) return base.RespondToMouseUp(sender, e);
            // A press that never moved is a click on the capsule, not a wire drag, and
            // Grasshopper answers it by selecting the component. Nothing of ours may
            // stand between the two: holding the wire on the cursor here is what made
            // the area around every input and output refuse to select anything.
            if (ShelfRuntime.Distance(origin, e.ControlLocation) < 6) return base.RespondToMouseUp(sender, e);
            if (Control.ModifierKeys != Keys.None) return base.RespondToMouseUp(sender, e);
            // Releasing on a port is a connection attempt, never a request for the
            // palette. The reach is Grasshopper's own, so the plug-in claims no more
            // room around a grip than Grasshopper does.
            if (document.FindAttributeByGrip(e.CanvasLocation, false, true, true, GripReach) != null)
                return base.RespondToMouseUp(sender, e);
            // A group counts as an attribute under the cursor, so releasing a wire onto
            // one used to fall through to native behaviour and never open the palette.
            var under = document.FindAttribute(e.CanvasLocation, true);
            if (under != null && !(under.DocObject is GH_Group)) return base.RespondToMouseUp(sender, e);
            var screen = sender.PointToScreen(e.ControlLocation); var position = e.CanvasLocation;
            sender.BeginInvoke(new Action(delegate {
                if (!sender.IsDisposed && sender.Document == document) ShelfRuntime.ShowPalette(sender, source, fromInput, position, screen);
            }));
            return GH_ObjectResponse.Release;
        }
    }
}



