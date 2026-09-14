using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using System;
using System.Drawing;
namespace WireShelf {
 internal sealed class CanvasLabels : IDisposable {
  readonly GH_Canvas canvas; readonly ToolboxSettings settings;
  internal CanvasLabels(GH_Canvas canvas,ToolboxSettings settings) { this.canvas=canvas;this.settings=settings;canvas.CanvasPostPaintObjects+=Paint; }
  void Paint(GH_Canvas sender) {
   if(canvas.Document==null || (!settings.ComponentNames && !settings.GroupNames))return;
   var g=canvas.Graphics;
   using(var ink=new SolidBrush(Ui.Ink))
   using(var font=new Font(GH_FontServer.StandardAdjusted,FontStyle.Italic))
   using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap }) {
    foreach(var obj in canvas.Document.Objects) {
     if(obj.Attributes==null)continue;
     var box=obj.Attributes.Bounds;
     if(settings.ComponentNames && (obj is IGH_Component || obj is IGH_Param)) {
      string text=settings.Nicknames?obj.NickName:obj.Name;
      if(String.IsNullOrWhiteSpace(text))continue;
      // Draw in GH document coordinates with the same native font/transform as
      // the input/output captions. Zoom scales text, position and spacing together.
      var size=g.MeasureString(text,font);
      var label=new RectangleF(box.Left+(box.Width-size.Width)/2-2,box.Top-size.Height-5,size.Width+4,size.Height+2);
      g.DrawString(text,font,ink,label,format);
     } else if(settings.GroupNames && obj is GH_Group && canvas.Viewport.Zoom<=settings.GroupZoom) {
      var state=g.Save();
      try {
       g.ResetTransform();g.PageUnit=GraphicsUnit.Pixel;g.PageScale=1;
       var top=canvas.Viewport.ProjectPoint(box.Location);
       using(var large=new Font("Segoe UI",settings.GroupTextSize,FontStyle.Bold,GraphicsUnit.Pixel))
       using(var paper=new SolidBrush(Color.FromArgb(220,Color.White))) {
        var label=new RectangleF(top.X,top.Y-large.Height-14,Math.Max(70,Math.Min(500,box.Width*canvas.Viewport.Zoom)),large.Height+10);
        g.FillRectangle(paper,label);g.DrawString(obj.NickName,large,ink,label,format);
       }
      } finally { g.Restore(state); }
     }
    }
   }
  }
  public void Dispose() { canvas.CanvasPostPaintObjects-=Paint;if(!canvas.IsDisposed)canvas.Invalidate(); }
 }
}

