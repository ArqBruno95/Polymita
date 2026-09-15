using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace Polymita {
 // One Rhino modelling aid, under the name Rhino gives it. Nothing is stored here:
 // every read and write goes straight to Rhino's own settings, so an aid switched
 // from Rhino's status bar, an F-key or a command reads correctly here, and one
 // switched here is the very setting Rhino acts on.
 internal sealed class ModelAid {
  internal readonly string Text,Tip;
  internal readonly OsnapModes Mode;
  readonly Func<bool> read; readonly Action<bool> write;
  internal ModelAid(string text,string tip,Func<bool> read,Action<bool> write) : this(text,tip,OsnapModes.None,read,write) { }
  internal ModelAid(string text,string tip,OsnapModes mode,Func<bool> read,Action<bool> write)
  { Text=text;Tip=tip;Mode=mode;this.read=read;this.write=write; }
  internal bool On { get { return read(); } set { write(value); } }
 }

 internal static class ModelAids {
  // Rhino's own Osnap toolbar, in its order and with its abbreviations.
  static readonly OsnapModes[] modes = {
   OsnapModes.End,OsnapModes.Near,OsnapModes.Point,OsnapModes.Midpoint,OsnapModes.Center,
   OsnapModes.Intersection,OsnapModes.Perpendicular,OsnapModes.Tangent,OsnapModes.Quadrant,
   OsnapModes.Knot,OsnapModes.Vertex };
  static readonly string[] names = { "End","Near","Point","Mid","Cen","Int","Perp","Tan","Quad","Knot","Vertex" };
  static bool Has(OsnapModes mode) { return (ModelAidSettings.OsnapModes & mode)==mode; }
  static void Set(OsnapModes mode,bool on) {
   ModelAidSettings.OsnapModes = on ? ModelAidSettings.OsnapModes|mode : ModelAidSettings.OsnapModes&~mode;
   // Turning a snap on with every snap disabled would otherwise do nothing
   // visible, which is how Rhino's own toolbar behaves in reverse: it lights up.
   if(on) ModelAidSettings.Osnap=true;
  }
  // Rhino's right-click on a snap: that one alone, everything else off.
  internal static void Only(OsnapModes mode) { ModelAidSettings.OsnapModes=mode;ModelAidSettings.Osnap=true; }
  internal static ModelAid[] Snaps() {
   var list=new List<ModelAid>();
   for(int i=0;i<modes.Length;i++) {
    var mode=modes[i];
    list.Add(new ModelAid(names[i],names[i]+" object snap. Right-click for this snap alone.",mode,
     delegate { return Has(mode); },delegate(bool on) { Set(mode,on); }));
   }
   list.Add(new ModelAid("Project","Project object snaps to the construction plane",
    delegate { return ModelAidSettings.ProjectSnapToCPlane; },delegate(bool on) { ModelAidSettings.ProjectSnapToCPlane=on; }));
   // Rhino's Disable reads as the opposite of the master switch, so the button is
   // lit exactly when the snaps are off.
   list.Add(new ModelAid("Disable","Suspend every object snap without losing the selection",
    delegate { return !ModelAidSettings.Osnap; },delegate(bool on) { ModelAidSettings.Osnap=!on; }));
   return list.ToArray();
  }
  internal static ModelAid[] Status() {
   return new[] {
    new ModelAid("Grid Snap","Snap the cursor to the construction grid",
     delegate { return ModelAidSettings.GridSnap; },delegate(bool on) { ModelAidSettings.GridSnap=on; }),
    new ModelAid("Ortho","Constrain the cursor to the ortho angle",
     delegate { return ModelAidSettings.Ortho; },delegate(bool on) { ModelAidSettings.Ortho=on; }),
    new ModelAid("Planar","Keep successive points on one plane",
     delegate { return ModelAidSettings.Planar; },delegate(bool on) { ModelAidSettings.Planar=on; }),
    new ModelAid("Osnap","The master switch for every object snap",
     delegate { return ModelAidSettings.Osnap; },delegate(bool on) { ModelAidSettings.Osnap=on; }),
    new ModelAid("SmartTrack","Temporary guide lines and points while picking",
     delegate { return SmartTrackSettings.UseSmartTrack; },delegate(bool on) { SmartTrackSettings.UseSmartTrack=on; }),
    new ModelAid("Gumball","Show the gumball on the current selection",
     delegate { return ModelAidSettings.AutoGumballEnabled; },delegate(bool on) { ModelAidSettings.AutoGumballEnabled=on; })
   };
  }
  // The document's own distance format, so a reading here matches one taken with
  // Rhino's Distance command rather than looking like a different measurement.
  internal static string Format(double distance) {
   var doc=RhinoDoc.ActiveDoc;
   if(doc==null) return distance.ToString("0.###");
   var digits=Math.Max(0,Math.Min(8,doc.ModelDistanceDisplayPrecision));
   return distance.ToString("F"+digits.ToString())+" "+doc.GetUnitSystemName(true,false,true,true);
  }
 }

 // A row of Rhino's toggles. It keeps no state of its own: every refresh reads
 // Rhino back, so the row cannot drift from what Rhino is actually doing.
 internal sealed class AidStrip : FlowLayoutPanel {
  readonly List<CheckBox> buttons=new List<CheckBox>();
  readonly List<ModelAid> aids=new List<ModelAid>();
  readonly ToolTip tips;
  bool loading;
  internal AidStrip(ModelAid[] entries,ToolTip tips) {
   this.tips=tips;
   Dock=DockStyle.Top;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;
   WrapContents=true;FlowDirection=FlowDirection.LeftToRight;Margin=Padding.Empty;Padding=Padding.Empty;
   foreach(var entry in entries) Add(entry);
   Sync();
  }
  void Add(ModelAid aid) {
   var button=new CheckBox { Text=aid.Text,Appearance=Appearance.Button,AutoSize=true,FlatStyle=FlatStyle.Flat,
    BackColor=Color.White,ForeColor=Ui.Ink,TextAlign=ContentAlignment.MiddleCenter,
    Margin=new Padding(0,0,3,3),Padding=new Padding(5,2,5,2),AccessibleName=aid.Text };
   button.FlatAppearance.BorderColor=Color.FromArgb(214,197,158);
   button.FlatAppearance.CheckedBackColor=Ui.Accent;
   tips.SetToolTip(button,aid.Tip);
   var captured=aid;
   button.CheckedChanged+=delegate { if(!loading) Ui.Safe(delegate { captured.On=button.Checked;Sync(); }); };
   if(captured.Mode!=OsnapModes.None)
    button.MouseUp+=delegate(object sender,MouseEventArgs e) {
     if(e.Button!=MouseButtons.Right)return;
     Ui.Safe(delegate { ModelAids.Only(captured.Mode);Sync(); });
    };
   buttons.Add(button);aids.Add(aid);Controls.Add(button);
  }
  // Called from the pane's timer as well as after a click, because Rhino's own
  // status bar, the F-keys and running commands all change these behind our back.
  // Nothing is written unless it actually differs, so a strip already in step
  // costs a handful of setting reads and no repaint at all.
  internal void Sync() {
   loading=true;
   try {
    for(int i=0;i<buttons.Count;i++) {
     var on=aids[i].On;
     if(buttons[i].Checked!=on) buttons[i].Checked=on;
     var ink=on?Color.White:Ui.Ink;
     if(buttons[i].ForeColor!=ink) buttons[i].ForeColor=ink;
    }
   }
   finally { loading=false; }
  }
 }

 // What Rhino shows in the distance pane while a point is being picked. Rhino
 // keeps no readable copy of that number, so it is measured here from the cursor
 // on the construction plane; once the command produces a curve, its real length
 // replaces the estimate, which is the figure a drawn line actually has.
 internal sealed class LengthReadout : MouseCallback {
  internal event Action<string> Changed;
  Point3d anchor;
  bool armed;
  Guid viewport;
  string reported;
  internal Guid Viewport { get { return viewport; } set { viewport=value; } }
  internal LengthReadout() {
   Command.EndCommand+=Ended;
   RhinoDoc.AddRhinoObject+=Added;
  }
  bool Ours(RhinoView view) { return view!=null && viewport!=Guid.Empty && view.ActiveViewportID==viewport; }
  static bool Project(RhinoView view,System.Drawing.Point client,out Point3d point) {
   point=Point3d.Unset;
   var port=view==null?null:view.ActiveViewport;
   if(port==null)return false;
   var ray=port.ClientToWorld(client);
   if(!ray.IsValid)return false;
   double t;
   if(!Intersection.LinePlane(ray,port.ConstructionPlane(),out t))return false;
   point=ray.PointAt(t);return true;
  }
  void Report(string text) { if(text==reported)return;reported=text;if(Changed!=null)Changed(text); }
  protected override void OnMouseDown(MouseCallbackEventArgs e) {
   base.OnMouseDown(e);
   if(e.MouseButton!=MouseButton.Left || !Ours(e.View))return;
   // Inside a command a left press is a picked point, and the span being measured
   // starts again from there. Outside one it is a selection, and there is nothing
   // to measure.
   if(!Command.InCommand()) { armed=false;Report(null);return; }
   Point3d point;
   if(Project(e.View,e.ViewportPoint,out point)) { anchor=point;armed=true; }
  }
  protected override void OnMouseMove(MouseCallbackEventArgs e) {
   base.OnMouseMove(e);
   if(!armed || !Ours(e.View))return;
   if(!Command.InCommand()) { armed=false;Report(null);return; }
   Point3d point;
   if(Project(e.View,e.ViewportPoint,out point)) Report(ModelAids.Format(anchor.DistanceTo(point)));
  }
  void Ended(object sender,CommandEventArgs e) { armed=false; }
  void Added(object sender,RhinoObjectEventArgs e) {
   if(!armed)return;
   var curve=e.TheObject==null?null:e.TheObject.Geometry as Curve;
   if(curve!=null) Report(ModelAids.Format(curve.GetLength()));
  }
  internal void Detach() {
   Enabled=false;
   Command.EndCommand-=Ended;
   RhinoDoc.AddRhinoObject-=Added;
  }
 }
}
