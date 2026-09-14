using System.Drawing;
using System.Drawing.Drawing2D;
namespace WireShelf
{
    internal static class Brand
    {
        internal const string Name = "Polymita";
        internal static readonly Bitmap Main = Make(0);
        internal static readonly Bitmap View = Make(1);
        internal static readonly Bitmap Library = Make(2);
        internal static readonly Bitmap Wires = Make(3);
        internal static readonly Bitmap Labels = Make(4);
        internal static readonly Bitmap Connect = Make(5);
        internal static readonly Bitmap Duplicate = Make(6);
        internal static readonly Bitmap Find = Make(7);
        private static Bitmap Make(int kind)
        {
            var bitmap = new Bitmap(24,24);
            using(var g = Graphics.FromImage(bitmap))
            using(var pen = new Pen(Color.FromArgb(191,66,44),2))
            using(var accent = new Pen(Color.Black,2))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; pen.LineJoin = LineJoin.Round;
                if(kind==0) {
                    using(var shell=new SolidBrush(Color.FromArgb(244,196,48))) g.FillEllipse(shell,2,3,15,15);
                    using(var spiral=new Pen(Color.FromArgb(191,66,44),1.65F)) {
                        var points=new System.Collections.Generic.List<PointF>();
                        for(int i=0;i<=48;i++) {
                            double t=i*System.Math.PI*3.5/48, r=0.35+0.58*t;
                            points.Add(new PointF(9.5F+(float)(r*System.Math.Cos(t)),10.5F+(float)(r*System.Math.Sin(t))));
                        }
                        g.DrawLines(spiral,points.ToArray());
                    }
                    accent.StartCap=LineCap.Round;accent.EndCap=LineCap.Round;
                    g.DrawLines(accent,new[]{new Point(2,20),new Point(17,20),new Point(21,16),new Point(21,12)});
                    g.DrawLine(accent,20,14,18,11);g.FillEllipse(Brushes.Black,20,9,2,2);
                }
                else if(kind==1) { g.DrawRectangle(pen,2,3,20,17); g.DrawLine(pen,2,7,22,7); g.DrawLines(accent,new[]{new Point(6,16),new Point(12,11),new Point(18,16)}); }
                else if(kind==2) { foreach(var x in new[]{3,13}) foreach(var y in new[]{3,13}) g.DrawRectangle(pen,x,y,7,7); g.DrawLine(accent,14,17,19,17); }
                else if(kind==3) { g.DrawLines(pen,new[]{new Point(3,5),new Point(18,5),new Point(18,19)}); g.DrawEllipse(accent,1,3,4,4); g.DrawEllipse(accent,16,17,4,4); }
                else if(kind==4) { g.DrawLine(pen,5,5,19,5); g.DrawLine(pen,12,5,12,20); }
                else if(kind==5) { g.DrawRectangle(pen,1,7,6,10); g.DrawRectangle(pen,17,7,6,10); g.DrawLine(accent,8,12,16,12); g.DrawLines(accent,new[]{new Point(12,9),new Point(16,12),new Point(12,15)}); }
                else if(kind==6) { g.DrawRectangle(pen,2,3,12,14); g.DrawRectangle(accent,9,8,12,14); }
                else {
                    // Magnifier in the painted-snail palette: shell-yellow lens, coral rim, dark ink handle.
                    using(var lens=new SolidBrush(Color.FromArgb(244,196,48))) g.FillEllipse(lens,3,3,12,12);
                    g.DrawEllipse(pen,3,3,12,12);
                    accent.StartCap=LineCap.Round; accent.EndCap=LineCap.Round;
                    g.DrawLine(accent,14,14,21,21);
                }
            }
            return bitmap;
        }
    }
}


