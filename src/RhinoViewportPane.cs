using Grasshopper.GUI.Canvas;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Polymita {
 // Rhino's own modelling aids around a real Rhino view, laid out where Rhino puts
 // them: the view and display pickers on top, the command line and its history
 // directly beneath them, and the object snaps, the status toggles and the
 // distance readout along the bottom.
 internal sealed class RhinoViewportPane : UserControl {
  internal readonly NativeRhinoViewHost ViewControl;
  readonly ComboBox views=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="View direction" };
  readonly ComboBox modes=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Display mode" };
  readonly ToolTip tips=new ToolTip();
  readonly Timer pulse=new Timer { Interval=200 };
  readonly RhinoCommandLine commandLine;
  readonly AidStrip snaps,status;
  readonly Label distance=new Label { Text="",TextAlign=ContentAlignment.MiddleCenter,AutoSize=false,
   Size=new Size(104,24),BorderStyle=BorderStyle.FixedSingle,BackColor=Color.White,
   Margin=new Padding(0,0,8,0),AccessibleName="Distance from the last picked point" };
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

   var bar=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(10,8,10,8),ColumnCount=2,RowCount=2 };
   bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   bar.Controls.Add(new Label {Text="VIEW",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,0,8,4)},0,0);
   bar.Controls.Add(new Label {Text="DISPLAY",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,0,0,4)},1,0);
   views.Dock=DockStyle.Fill;modes.Dock=DockStyle.Fill;views.Margin=new Padding(0,0,8,0);modes.Margin=Padding.Empty;
   bar.Controls.Add(views,0,1);bar.Controls.Add(modes,1,1);
   views.DropDownWidth=180;modes.DropDownWidth=280;

   commandLine=new RhinoCommandLine(tips,delegate { return ViewControl.HasView; },
    delegate { ViewControl.ActivateView(); },Fit);

   snaps=new AidStrip(ModelAids.Snaps(),tips);
   status=new AidStrip(ModelAids.Status(),tips);
   tips.SetToolTip(distance,"Distance from the last point picked in this view, in the document's units.");

   var readouts=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=1,Margin=Padding.Empty,Padding=Padding.Empty };
   readouts.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   readouts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   readouts.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   readouts.Controls.Add(distance,0,0);readouts.Controls.Add(status,1,0);

   var footer=new TableLayoutPanel { Dock=DockStyle.Bottom,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=2,Padding=new Padding(10,6,10,8) };
   footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   footer.Controls.Add(snaps,0,0);footer.Controls.Add(readouts,0,1);

   // Docking is resolved from the last control added to the first, so the order
   // here reads backwards: the bar claims the top edge, the command line the strip
   // below it, the footer the bottom, and the view keeps what is left.
   Controls.Add(ViewControl);Controls.Add(footer);Controls.Add(commandLine);Controls.Add(bar);

   views.SelectedIndexChanged+=delegate { Ui.Safe(ApplyView); };
   modes.SelectedIndexChanged+=delegate { Ui.Safe(ApplyMode); };
   ViewControl.Ready+=delegate { ApplyView();ApplyMode();length.Viewport=ViewControl.ViewportId; };
   length.Changed+=delegate(string text) { distance.Text=text==null?"":text; };
   length.Enabled=true;
   // Rhino announces neither a prompt change nor a toggled aid, so the panel asks.
   // Every read is a property on Rhino's settings and nothing is written unless it
   // actually differs, so an idle pane does no work Rhino can see.
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

  void Poll() {
   var id=ViewControl.ViewportId;
   if(length.Viewport!=id)length.Viewport=id;
   commandLine.Poll();
   snaps.Sync();
   status.Sync();
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
