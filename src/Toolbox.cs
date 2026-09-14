using Grasshopper;
using Grasshopper.GUI.Canvas;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace WireShelf {
internal sealed class Toolbox : IDisposable {
 readonly GH_Canvas canvas; readonly ToolboxSettings settings; readonly string path;
 readonly Dictionary<int,Form> windows=new Dictionary<int,Form>();
 RhinoViewportPane viewport; Splitter splitter; Timer repaint;
 internal bool ViewportVisible { get { return viewport!=null; } }
 internal Toolbox(GH_Canvas canvas,ToolboxSettings settings,string path) { this.canvas=canvas; this.settings=settings; this.path=path; }
 void Save() { Ui.Safe(()=>settings.Save(path)); }
 internal void SelectTab(int tab) {
  if(tab==0) { ToggleViewport(); return; }
  Form existing; if(windows.TryGetValue(tab,out existing)) { existing.Activate(); return; }
  var window=new Form(); Ui.Style(window,Brand.Name+(tab==1?" · Find / Profiler":tab==2?" · Wires":" · Labels"),new Size(450,480)); window.ShowInTaskbar=false;
  if(tab==1) {
   var finder=new ComponentFinder(canvas) { Dock=DockStyle.Fill }; window.Controls.Add(finder);
   var timer=new Timer { Interval=500 }; timer.Tick+=delegate { if(window.Visible) finder.RefreshResults(false); };
   window.Shown+=delegate { timer.Start(); }; window.FormClosed+=delegate { timer.Dispose(); };
  } else window.Controls.Add(tab==2?WireOptions():LabelOptions());
  windows.Add(tab,window); window.FormClosed+=delegate { windows.Remove(tab); Save(); }; window.Show(Instances.DocumentEditor);
 }
 void ToggleViewport() {
  if(viewport!=null) { HideViewport(); return; }
  var parent=canvas.Parent; if(parent==null) return;
  viewport=new RhinoViewportPane(canvas,settings) { Dock=DockStyle.Left,Width=settings.PanelWidth };
  splitter=new Splitter { Dock=DockStyle.Left,Width=5,MinSize=260,MinExtra=260 };
  parent.Controls.Add(splitter); parent.Controls.Add(viewport); parent.Controls.SetChildIndex(splitter,1); parent.Controls.SetChildIndex(viewport,2);
  repaint=new Timer { Interval=350 }; repaint.Tick+=delegate { if(viewport!=null && viewport.Visible) viewport.ViewControl.RedrawView(); };
  repaint.Start(); parent.PerformLayout(); ShelfRuntime.RefreshToolbar();
 }
 void HideViewport() {
  if(viewport==null) return; settings.PanelWidth=viewport.Width; Save(); repaint.Dispose(); viewport.Dispose(); splitter.Dispose(); viewport=null; splitter=null;
  if(!canvas.IsDisposed && canvas.Parent!=null) canvas.Parent.PerformLayout(); ShelfRuntime.RefreshToolbar();
 }
 static FlowLayoutPanel Options() { return new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(16) }; }
 static NumericUpDown Number(decimal value,decimal min,decimal max) { return new NumericUpDown { Minimum=min,Maximum=max,Value=value,Width=100 }; }
 Control WireOptions() {
  var panel=Options();
  // A failure while loading at start-up is reported here rather than as a dialog on
  // every start, now that segmented wires are on by default.
  if(WireStyles.LastFailure!=null && !WireStyles.Polylines)
   panel.Controls.Add(new Label { Text="Segmented wires are unavailable: "+WireStyles.LastFailure,AutoSize=true,ForeColor=Ui.Accent,MaximumSize=new Size(390,0),Margin=new Padding(0,0,0,10) });
  var enabled=new CheckBox { Text="Draw segmented wires",AutoSize=true,Checked=settings.Polylines && WireStyles.Polylines };
  var variant=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=370,AccessibleName="Wire style" };
  variant.Items.AddRange(new object[] { "Two orthogonal segments","Adaptive three-segment wire" }); variant.SelectedIndex=settings.WireVariant;
  var highlight=new CheckBox { Text="Highlight selected connections",AutoSize=true,Checked=settings.Highlight };
  var color=Ui.Button("Selection color…",delegate {
   using(var dialog=new ColorDialog { Color=Color.FromArgb(settings.SelectedArgb),FullOpen=true })
   if(dialog.ShowDialog(Instances.DocumentEditor)==DialogResult.OK) { settings.SelectedArgb=dialog.Color.ToArgb(); WireStyles.SetHighlight(settings.Highlight,dialog.Color); Save(); canvas.Invalidate(); }
  });
  Action apply=delegate {
   settings.Polylines=enabled.Checked; settings.WireVariant=variant.SelectedIndex; settings.Highlight=highlight.Checked;
   WireStyles.Variant=settings.WireVariant;
   try { WireStyles.SetPolylines(settings.Polylines); } catch(Exception ex) { enabled.Checked=false; Ui.Error(ex); }
   WireStyles.SetHighlight(settings.Highlight,Color.FromArgb(settings.SelectedArgb)); Save(); canvas.Invalidate();
  };
  enabled.CheckedChanged+=delegate { apply(); }; variant.SelectedIndexChanged+=delegate { apply(); }; highlight.CheckedChanged+=delegate { apply(); };
 
  panel.Controls.AddRange(new Control[] { enabled,variant,
   new Label { Text="Adaptive: horizontal ends and an automatic diagonal.\nAligned ports may produce a straight wire.",AutoSize=true,Margin=new Padding(0,10,0,16) },highlight,color }); return panel;
 }
 Control LabelOptions() {
  var panel=Options();
  var names=new CheckBox { Text="Show component names above objects",AutoSize=true,Checked=settings.ComponentNames };
  var nicknames=new CheckBox { Text="Use nicknames",AutoSize=true,Checked=settings.Nicknames };
  var groups=new CheckBox { Text="Large group titles when zoomed out",AutoSize=true,Checked=settings.GroupNames };
  var groupSize=Number((decimal)settings.GroupTextSize,14,72); groupSize.AccessibleName="Group title size";
  var zoom=Number((decimal)settings.GroupZoom*100,10,100); zoom.AccessibleName="Zoom threshold as a percentage";
  Action apply=delegate { settings.ComponentNames=names.Checked; settings.Nicknames=nicknames.Checked; settings.GroupNames=groups.Checked; settings.GroupTextSize=(float)groupSize.Value; settings.GroupZoom=(float)zoom.Value/100; Save(); canvas.Invalidate(); };
  names.CheckedChanged+=delegate { apply(); }; nicknames.CheckedChanged+=delegate { apply(); }; groups.CheckedChanged+=delegate { apply(); }; groupSize.ValueChanged+=delegate { apply(); }; zoom.ValueChanged+=delegate { apply(); };
  panel.Controls.AddRange(new Control[] { names,nicknames,new Label { Text="Native Grasshopper font · scales with the canvas",AutoSize=true },groups,new Label { Text="Group title size (px)",AutoSize=true },groupSize,new Label { Text="Show group titles below this zoom (%)",AutoSize=true },zoom }); return panel;
 }
 public void Dispose() { foreach(var form in windows.Values.ToArray()) form.Close(); HideViewport(); }
}}


