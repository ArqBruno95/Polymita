using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using System;
using System.Collections.Generic;
using System.Drawing;
namespace WireShelf {
 internal sealed class CanvasLabels : IDisposable {
  readonly GH_Canvas canvas; readonly ToolboxSettings settings;
  // This runs on every canvas repaint. Measuring each caption again and rebuilding
  // the fonts once per frame cost more than drawing them, so both are kept and
  // dropped only when something that changes a measurement changes.
  readonly Dictionary<string,SizeF> measured=new Dictionary<string,SizeF>();
  Font italic,large; string sourceName; float sourceSize=-1,largeSize=-1,zoom=-1,scale=-1; FontStyle sourceStyle;
  internal CanvasLabels(GH_Canvas canvas,ToolboxSettings settings) { this.canvas=canvas;this.settings=settings;canvas.CanvasPostPaintObjects+=Paint; }
  // Compared by value: whether GH_FontServer hands back the same instance is its own business.
  Font Italic(Graphics g) {
   var current=GH_FontServer.StandardAdjusted;
   if(italic==null || sourceSize!=current.Size || sourceName!=current.Name || sourceStyle!=current.Style) {
    if(italic!=null)italic.Dispose();
    sourceName=current.Name;sourceSize=current.Size;sourceStyle=current.Style;
    italic=new Font(current,FontStyle.Italic);measured.Clear();
   }
   if(zoom!=canvas.Viewport.Zoom || scale!=g.PageScale) { zoom=canvas.Viewport.Zoom;scale=g.PageScale;measured.Clear(); }
   return italic;
  }
  SizeF Measure(Graphics g,string text) {
   SizeF size;
   if(measured.TryGetValue(text,out size))return size;
   size=g.MeasureString(text,italic);
   if(measured.Count>2048)measured.Clear();
   measured[text]=size;return size;
  }
  Font Large() {
   if(large==null || largeSize!=settings.GroupTextSize) {
    if(large!=null)large.Dispose();
    largeSize=settings.GroupTextSize;large=new Font("Segoe UI",largeSize,FontStyle.Bold,GraphicsUnit.Pixel);
   }
   return large;
  }
  void Paint(GH_Canvas sender) {
   if(canvas.Document==null || (!settings.ComponentNames && !settings.GroupNames))return;
   var g=canvas.Graphics;
   var font=Italic(g);
   // A caption sits above its object, and that offset is a fixed number of screen
   // pixels, so in document units the margin has to grow as the canvas zooms out.
   var region=canvas.Viewport.VisibleRegion;
   var margin=200F/Math.Max(0.05F,canvas.Viewport.Zoom);
   region.Inflate(margin,margin);
   using(var ink=new SolidBrush(Ui.Ink))
   using(var paper=new SolidBrush(Color.FromArgb(220,Color.White)))
   using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap }) {
    foreach(var obj in canvas.Document.Objects) {
     if(obj.Attributes==null)continue;
     var box=obj.Attributes.Bounds;
     if(!region.IntersectsWith(box))continue;
     if(settings.ComponentNames && (obj is IGH_Component || obj is IGH_Param)) {
      string text=settings.Nicknames?obj.NickName:obj.Name;
      if(String.IsNullOrWhiteSpace(text))continue;
      // Draw in GH document coordinates with the same native font/transform as
      // the input/output captions. Zoom scales text, position and spacing together.
      var size=Measure(g,text);
      var label=new RectangleF(box.Left+(box.Width-size.Width)/2-2,box.Top-size.Height-5,size.Width+4,size.Height+2);
      g.DrawString(text,font,ink,label,format);
     } else if(settings.GroupNames && obj is GH_Group && canvas.Viewport.Zoom<=settings.GroupZoom) {
      var state=g.Save();
      try {
       g.ResetTransform();g.PageUnit=GraphicsUnit.Pixel;g.PageScale=1;
       var top=canvas.Viewport.ProjectPoint(box.Location);
       var title=Large();
       var label=new RectangleF(top.X,top.Y-title.Height-14,Math.Max(70,Math.Min(500,box.Width*canvas.Viewport.Zoom)),title.Height+10);
       g.FillRectangle(paper,label);g.DrawString(obj.NickName,title,ink,label,format);
      } finally { g.Restore(state); }
     }
    }
   }
  }
  public void Dispose() {
   canvas.CanvasPostPaintObjects-=Paint;
   if(italic!=null){italic.Dispose();italic=null;}
   if(large!=null){large.Dispose();large=null;}
   if(!canvas.IsDisposed)canvas.Invalidate();
  }
 }
}
