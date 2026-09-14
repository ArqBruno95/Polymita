using Grasshopper.GUI.Canvas;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace WireShelf {
 internal sealed class RhinoViewportPane : UserControl {
  internal readonly NativeRhinoViewHost ViewControl;
  readonly ComboBox views=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="View direction" };
  readonly ComboBox modes=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Display mode" };
  readonly TextBox command=new SearchBox { Hint="Command or option…",Anchor=AnchorStyles.Left|AnchorStyles.Right,AccessibleName="Rhino command or option" };
  readonly Label prompt=new Label { Dock=DockStyle.Fill,AutoSize=true,Text="Rhino command",Margin=new Padding(0,0,0,6) };
  readonly ToolTip tips=new ToolTip();
  readonly Timer promptTimer=new Timer { Interval=100 };
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
   var bar=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(10,8,10,10),ColumnCount=2,RowCount=2 };
   bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   bar.Controls.Add(new Label {Text="VIEW",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,0,8,4)},0,0);
   bar.Controls.Add(new Label {Text="DISPLAY",AutoSize=true,ForeColor=Ui.Muted,Margin=new Padding(0,0,0,4)},1,0);
   views.Dock=DockStyle.Fill;modes.Dock=DockStyle.Fill;views.Margin=new Padding(0,0,8,0);modes.Margin=Padding.Empty;
   bar.Controls.Add(views,0,1);bar.Controls.Add(modes,1,1);
   views.DropDownWidth=180;modes.DropDownWidth=280;
   var bottom=new TableLayoutPanel { Dock=DockStyle.Bottom,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(10,8,10,10),ColumnCount=3,RowCount=2 };
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   bottom.Controls.Add(prompt,0,0);bottom.SetColumnSpan(prompt,3);
   var run=Ui.Button("Run",SendCommand,true);run.MinimumSize=new Size(44,30);run.Margin=new Padding(6,0,0,0);
   var fit=Ui.Button("Fit",Fit);fit.MinimumSize=new Size(40,30);fit.Margin=new Padding(6,0,0,0);
   command.Margin=Padding.Empty;
   bottom.Controls.Add(command,0,1);bottom.Controls.Add(run,1,1);bottom.Controls.Add(fit,2,1);
   tips.SetToolTip(command,"Enter: submit a Rhino command, coordinate or option. Escape: cancel.");tips.SetToolTip(fit,"Frame all Rhino and Grasshopper geometry. Use Zoom Selected to focus the selection.");
   bottom.SizeChanged+=delegate { prompt.MaximumSize=new Size(Math.Max(100,bottom.ClientSize.Width-bottom.Padding.Horizontal),0); };
   Controls.Add(ViewControl);Controls.Add(bottom);Controls.Add(bar);
   views.SelectedIndexChanged+=delegate { Ui.Safe(ApplyView); };
   modes.SelectedIndexChanged+=delegate { Ui.Safe(ApplyMode); };
   ViewControl.Ready+=delegate { ApplyView();ApplyMode(); };
   command.KeyDown+=delegate(object sender,KeyEventArgs e) {
    if(e.KeyCode==Keys.Enter) {
     e.SuppressKeyPress=true;
     SendCommand();
    } else if(e.KeyCode==Keys.Escape) {
     e.SuppressKeyPress=true;command.Clear();ViewControl.ActivateView();RhinoApp.SendKeystrokes("\x1b",false);
    }
   };
   promptTimer.Tick+=delegate { prompt.Text=RhinoApp.CommandPrompt; };
   promptTimer.Start();
  }
  void SendCommand() {
   if(!ViewControl.HasView)return;
   string text=command.Text.Trim();command.Clear();ViewControl.ActivateView();RhinoApp.SendKeystrokes(text,true);
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
  internal void CopyRhinoView() {
   var doc=RhinoDoc.ActiveDoc;
   if(!ViewControl.HasView || doc==null || doc.Views.ActiveView==null)return;
   using(var info=new Rhino.DocObjects.ViewportInfo(doc.Views.ActiveView.ActiveViewport))ViewControl.Viewport.SetViewProjection(info,true);
   ViewControl.RedrawView();
  }
  internal void Fit() {
   if(!ViewControl.HasView)return;
   var bounds=BoundingBox.Empty;
   if(canvas.Document!=null)bounds.Union(canvas.Document.PreviewBoundingBox);
   var doc=RhinoDoc.ActiveDoc;if(doc!=null)bounds.Union(doc.Objects.BoundingBoxVisible);
   if(bounds.IsValid)ViewControl.Viewport.ZoomBoundingBox(bounds);else ViewControl.Viewport.ZoomExtents();
   ViewControl.RedrawView();
  }
  protected override void Dispose(bool disposing) { if(disposing) { promptTimer.Dispose();tips.Dispose(); }base.Dispose(disposing); }
 }
}

