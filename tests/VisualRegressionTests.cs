using System;
using System.Drawing;
using System.IO;
using Grasshopper.Kernel;
using WireShelf;
public static class VisualRegressionTests {
 static int count;static TextWriter log;
 static void Check(bool x,string m){if(!x)throw new Exception(m);count++;log.WriteLine("PASS "+m);}
 static int Height(Font font,float zoom,float dpi) {
  using(var bitmap=new Bitmap(600,180)) {
   bitmap.SetResolution(dpi,dpi);
   using(var g=Graphics.FromImage(bitmap)) {g.ScaleTransform(zoom,zoom);g.DrawString("Sphere",font,Brushes.Black,5,5);}
   int top=180,bottom=-1;
   for(int y=0;y<180;y++)for(int x=0;x<600;x++)if(bitmap.GetPixel(x,y).A>0){top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
   return bottom-top+1;
  }
 }
 public static void Run(string root) {
  count=0;using(log=new StreamWriter(Path.Combine(root,"test-output","visual-regression.txt"))) {
   using(var label=new Font(GH_FontServer.StandardAdjusted,FontStyle.Italic)) {
    foreach(float dpi in new[]{96F,120F,144F}) {
     int previous=0;
     foreach(float zoom in new[]{.5F,1F,1.73F,2.82F}) {
      int title=Height(label,zoom,dpi),port=Height(GH_FontServer.StandardAdjusted,zoom,dpi);
      Check(title>previous,"Titles scale with zoom "+zoom+" at "+dpi+" DPI");previous=title;
      Check(Math.Abs(title-port)<=3,"Title and port glyph heights agree at zoom "+zoom+" DPI "+dpi);
     }
    }
   }
   Check(Brand.Name=="Polymita","New brand name");
   foreach(var icon in new[]{Brand.Main,Brand.Labels,Brand.Find})Check(icon.Width==24&&icon.Height==24,"Native toolbar icon dimensions");
   Brand.Find.Save(Path.Combine(root,"test-output","Polymita-find-icon.png"));
   Brand.Main.Save(Path.Combine(root,"test-output","Polymita-icon.png"));
   Brand.Labels.Save(Path.Combine(root,"test-output","Polymita-labels-icon.png"));
   log.WriteLine(count+" visual checks passed.");
  }
 }
}
