using Grasshopper.GUI.Canvas;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Polymita {
 // A real Rhino view with Rhino's own command line and modelling aids around it,
 // laid out where Rhino puts them. Both strips are folded away until asked for:
 // the viewport is what this panel is for, and everything else is one arrow wide
 // until the arrow is clicked, after which it stays open until it is clicked back.
 internal sealed class RhinoViewportPane : UserControl {
  internal readonly NativeRhinoViewHost ViewControl;
  readonly ComboBox views=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="View direction" };
  readonly ComboBox modes=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Display mode" };
  readonly ToolTip tips=new ToolTip();
  readonly Timer pulse=new Timer { Interval=200 };
  readonly RhinoCommandLine commandLine;
  readonly AidStrip aids;
  readonly Disclosure commandArrow,aidsArrow;
  readonly Label distance=new Label { Text="",TextAlign=ContentAlignment.MiddleCenter,AutoSize=false,
   Size=new Size(86,20),BorderStyle=BorderStyle.FixedSingle,BackColor=Color.White,
   Font=new Font("Segoe UI",8F),AccessibleName="Distance from the last picked point" };
  readonly LengthReadout length=new LengthReadout();
  readonly GH_Canvas canvas; readonly ToolboxSettings settings;

  internal RhinoViewportPane(GH_Canvas canvas,ToolboxSettings settings) {
   this.canvas=canvas;this.settings=settings;Dock=DockStyle.Fill;
   Font=new Font("Segoe UI",9);BackColor=Ui.Paper;ForeColor=Ui.Ink;AutoScaleMode=AutoScaleMode.Dpi;
   ViewControl=new NativeRhinoViewHost { Dock=DockStyle.Fill };
   views.Items.AddRange(new object[]{"Perspective","Top","Front","Right","Left","Back","Bottom","Two-point"});
   views.SelectedItem=settings.View=="TwoPointPerspective"?"Two-point":views.Items.Contains(settings.View??"")?settings.View:"Perspective";
   var available=DisplayModeDescription.GetDisplayModes();modes.DisplayMember="LocalName";
   foreach(var mode in available)modes.Items.Add(mode);
   modes.SelectedItem=available.FirstOrDefault(m=>m.Id==settings.DisplayMode)??available.FirstOrDefault(m=>m.Id==DisplayModeDescription.ShadedId);

   var bar=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(10,8,10,6),ColumnCount=2,RowCount=2 };
   bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   bar.Controls.Add(new Label {Text="VIEW",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,0,8,4)},0,0);
   bar.Controls.Add(new Label {Text="DISPLAY",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,0,0,4)},1,0);
   views.Dock=DockStyle.Fill;modes.Dock=DockStyle.Fill;views.Margin=new Padding(0,0,8,0);modes.Margin=Padding.Empty;
   bar.Controls.Add(views,0,1);bar.Controls.Add(modes,1,1);
   views.DropDownWidth=180;modes.DropDownWidth=280;

   commandLine=new RhinoCommandLine(tips,delegate { return ViewControl.HasView; },delegate { ViewControl.ActivateView(); });
   commandArrow=new Disclosure("Command",settings.ShowCommandLine,tips,"Show or hide Rhino's command line and its history.");
   commandArrow.Dock=DockStyle.Top;
   commandLine.Visible=commandArrow.Open;

   // One row, grouped by subject with a hairline between groups: the snaps, then
   // the status switches, then the distance Rhino would show in its own pane.
   aids=new AidStrip(tips);
   aids.AddGroup(ModelAids.Snaps());
   aids.AddGroup(ModelAids.Status());
   aids.AddReadout(distance);
   tips.SetToolTip(distance,"Distance from the last point picked in this view, in the document's units.");
   aidsArrow=new Disclosure("Osnap, Ortho, distance",settings.ShowModelAids,tips,"Show or hide Rhino's object snaps, status switches and distance readout.");
   aidsArrow.Dock=DockStyle.Bottom;
   aids.Dock=DockStyle.Bottom;aids.Padding=new Padding(8,2,8,2);
   aids.Visible=aidsArrow.Open;

   // Everything docks straight onto the panel. Nesting each strip with its arrow
   // inside its own auto-sizing table put three levels of negotiated height
   // between a folded strip and this panel, and a strip that comes back has to
   // claim its height without any of that having to agree first.
   // Docking is resolved from the last control added to the first, so the order
   // here reads backwards: the pickers claim the top edge, then the command
   // arrow and the command line, then the aids arrow at the very bottom with the
   // aids above it, and the view keeps all the rest. A hidden control is skipped
   // outright, so a folded strip costs nothing at all.
   Controls.Add(ViewControl);Controls.Add(aids);Controls.Add(aidsArrow);
   Controls.Add(commandLine);Controls.Add(commandArrow);Controls.Add(bar);

   views.SelectedIndexChanged+=delegate { Ui.Safe(ApplyView); };
   modes.SelectedIndexChanged+=delegate { Ui.Safe(ApplyMode); };
   ViewControl.Ready+=delegate { ApplyView();ApplyMode();length.Viewport=ViewControl.ViewportId; };
   length.Changed+=delegate(string text) { distance.Text=text==null?"":text; };
   length.Enabled=aidsArrow.Open;
   commandArrow.Toggled+=delegate { Ui.Safe(delegate {
    Reveal(commandLine,commandArrow.Open);
    settings.ShowCommandLine=commandArrow.Open;ShelfRuntime.SaveToolboxSettings(); }); };
   aidsArrow.Toggled+=delegate { Ui.Safe(delegate {
    Reveal(aids,aidsArrow.Open);
    // No reason to watch every mouse move in every Rhino view while the readout
    // that would show the result is folded away.
    length.Enabled=aidsArrow.Open;
    settings.ShowModelAids=aidsArrow.Open;ShelfRuntime.SaveToolboxSettings(); }); };
   // Rhino announces neither a prompt change nor a toggled aid, so the panel asks.
   // A folded strip is asked nothing at all.
   pulse.Tick+=delegate {
    try { Poll(); }
    catch(Exception ex) {
     // A dialog several times a second would bury Grasshopper. Stop asking and
     // say it once; the view, the pickers and the command box all still work.
     pulse.Stop();
     RhinoApp.WriteLine("Polymita stopped following Rhino's command line and modelling aids: "+ex.Message);
    }
   };
   pulse.Start();
  }

  // Show or fold a strip and settle the panel around it in the same breath. The
  // docked view has to give the room back, and asking for the layout outright is
  // cheaper than trusting a change of visibility to travel on its own.
  void Reveal(Control strip,bool open) {
   strip.Visible=open;
   PerformLayout();
   ViewControl.Sync();
  }

  void Poll() {
   var id=ViewControl.ViewportId;
   if(length.Viewport!=id)length.Viewport=id;
   if(commandArrow.Open)commandLine.Poll();
   if(aidsArrow.Open)aids.Sync();
  }

  internal void ApplyView() {
   settings.View=(string)views.SelectedItem=="Two-point"?"TwoPointPerspective":(string)views.SelectedItem;
   if(!ViewControl.HasView)return;
   ViewControl.Viewport.SetProjection((DefinedViewportProjection)Enum.Parse(typeof(DefinedViewportProjection),settings.View),"Polymita",true);Fit();
  }
  internal void ApplyMode() {
   var mode=modes.SelectedItem as DisplayModeDescription;if(mode==null)return;
   settings.DisplayMode=mode.Id;
   if(ViewControl.HasView) { ViewControl.Viewport.DisplayMode=mode;ViewControl.RedrawView(); }
  }
  // Frames Grasshopper's preview together with Rhino's own geometry, which no
  // single Rhino command does. Used when the view direction changes.
  internal void Fit() {
   if(!ViewControl.HasView)return;
   var bounds=BoundingBox.Empty;
   if(canvas.Document!=null)bounds.Union(canvas.Document.PreviewBoundingBox);
   var doc=RhinoDoc.ActiveDoc;if(doc!=null)bounds.Union(doc.Objects.BoundingBoxVisible);
   if(bounds.IsValid)ViewControl.Viewport.ZoomBoundingBox(bounds);else ViewControl.Viewport.ZoomExtents();
   ViewControl.RedrawView();
  }
  protected override void Dispose(bool disposing) { if(disposing) { pulse.Dispose();length.Detach();tips.Dispose(); }base.Dispose(disposing); }
 }
}
