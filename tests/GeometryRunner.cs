using System;
using System.IO;
using System.Reflection;
public static class GeometryRunner
{
    [STAThread] public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender,ResolveEventArgs e) {
            var name=new AssemblyName(e.Name).Name+".dll";
            foreach(var folder in new[]{@"C:\Program Files\Rhino 8\Plug-ins\Grasshopper",@"C:\Program Files\Rhino 8\System"})
            { var path=Path.Combine(folder,name); if(File.Exists(path)) return Assembly.LoadFrom(path); }
            return null;
        };
        try
        {
            var assembly=Assembly.LoadFrom(args[0]);
            assembly.GetType("Regression085Tests").GetMethod("RunGeometry").Invoke(null,new object[]{Console.Out});
            return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
