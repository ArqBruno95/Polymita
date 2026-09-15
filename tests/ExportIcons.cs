using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Polymita;
class ExportIcons
{
    static void Main(string[] args)
    {
        var names=new[]{"polymita-main","tool-viewport","tool-library","tool-wires","tool-labels","tool-find"};
        var icons=new[]{Brand.Main,Brand.View,Brand.Library,Brand.Wires,Brand.Labels,Brand.Find};
        Directory.CreateDirectory(args[0]);
        using(var sheet=new Bitmap(680,190))
        using(var g=Graphics.FromImage(sheet))
        using(var font=new Font("Segoe UI",8))
        {
            g.Clear(Color.White);
            for(int i=0;i<icons.Length;i++)
            {
                icons[i].Save(Path.Combine(args[0],names[i]+"-24.png"),ImageFormat.Png);
                using(var large=Brand.Make(i,96))
                {
                    large.Save(Path.Combine(args[0],names[i]+"-96.png"),ImageFormat.Png);
                    g.DrawImageUnscaled(large,10+i*112,32);
                }
                g.DrawString(names[i].Replace('-', ' '),font,Brushes.DimGray,10+i*112,10);
                g.DrawImageUnscaled(icons[i],45+i*112,139);
            }
            g.DrawString("Polymita 0.8.5 - six current icons; native 24 px below",font,Brushes.DimGray,10,173);
            sheet.Save(Path.Combine(args[0],"contact-sheet.png"),ImageFormat.Png);
        }
    }
}
