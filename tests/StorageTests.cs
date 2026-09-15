using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Polymita;

public static class StorageTests
{
    static int count;
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); count++; Console.WriteLine("PASS "+name); }
    public static int Main()
    {
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"storage-fixtures-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach(var name in new[]{"WireShelf","Zunzun","Zunzún","Nitido","Nítido"})
            {
                var folder=Path.Combine(root,Guid.NewGuid().ToString("N"));
                var old=Path.Combine(folder,name); Directory.CreateDirectory(old);
                var path=Path.Combine(old,"library.wireshelf.json");
                var library=new ShelfLibrary();
                var section=new ShelfSection { Title="Mis favoritos" }; library.Sections.Add(section);
                var recipe=new ShelfItem { ComponentId=Guid.NewGuid(),Name="Mi receta",SnapshotXml="<chunk name=\"WireShelf\"><value>unaltered</value></chunk>" };
                section.Items.Add(recipe);
                var store=new LibraryStore(path); store.Save(library); store.Save(library);
                var settings=new ToolboxSettings { CutWires=false, SnapAlign=true, PanelWidth=530, Shortcuts=new[]{"duplicate=262225"} };
                settings.Save(Path.Combine(old,"toolbox.json"));
                var migration=new SettingsStorage(folder); migration.Prepare();
                Check(migration.DirectoryPath==Path.Combine(folder,"Polymita"),name+": new directory uses current brand");
                Check(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(migration.LibraryPath)),name+": migration is byte-for-byte, including recipe payloads");
                Check(File.Exists(migration.LibraryPath+".bak") && File.Exists(path),name+": backups and originals retained");
                Check(new LibraryStore(migration.LibraryPath).Load().Sections[0].Items[0].Id==recipe.Id,name+": IDs preserved across namespace change");
                var loaded=ToolboxSettings.Load(migration.ToolboxPath);
                Check(!loaded.CutWires && loaded.SnapAlign && loaded.PanelWidth==530 && loaded.Shortcuts.SequenceEqual(settings.Shortcuts),name+": settings and custom shortcuts preserved");
                library.Sections[0].Title="New Polymita value"; new LibraryStore(migration.LibraryPath).Save(library);
                new SettingsStorage(folder).Prepare();
                Check(new LibraryStore(migration.LibraryPath).Load().Sections[0].Title=="New Polymita value",name+": repeated migration never overwrites current data");
                File.WriteAllText(migration.LibraryPath,"broken");
                var recovered=new LibraryStore(migration.LibraryPath);
                Check(recovered.Load().Sections[0].Items[0].Id==recipe.Id && recovered.Recovered,name+": backup recovery works at new path");
            }
            var fresh=Path.Combine(root,"fresh"); var clean=new SettingsStorage(fresh); clean.Prepare();
            new ToolboxSettings().Save(clean.ToolboxPath);
            Check(Directory.GetDirectories(fresh).Select(Path.GetFileName).SequenceEqual(new[]{"Polymita"}),"Clean installation creates only the Polymita settings folder");
            var mixed=new ShelfSection();
            mixed.Items.Add(new ShelfItem { ComponentId=Guid.NewGuid() });
            mixed.Items.Add(new ShelfItem { ActionId="connect",Name="Connect selection" });
            mixed.Items.Add(new ShelfItem { ActionId="duplicate",Name="Duplicate selection" });
            mixed.Items.Add(new ShelfItem { ComponentId=Guid.NewGuid() });
            var grid=new ShelfGridLayout(new[]{mixed},3,1);
            Check(grid.Cells.Where(c=>c.Item.IsAction).All(c=>c.Bounds.Width==108),"Existing operation favorites are full-width text rows");
            Check(grid.Cells.All(c=>c.Bounds.Bottom<=grid.Size.Height) && !grid.Cells.SelectMany((c,i)=>grid.Cells.Skip(i+1).Where(o=>o.Bounds.IntersectsWith(c.Bounds))).Any(),"Text actions and component icons have separate nonoverlapping hit targets");
            Check(grid.Cells.Select(c=>c.Item.Id).SequenceEqual(mixed.Items.Select(i=>i.Id)),"Operation favorites retain their saved position and identity");
            Console.WriteLine(count+" storage and action-layout checks passed."); return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex); return 1; }
        // Retain fixtures under test-output for inspection; no user settings are read.
    }
}
