using System;
using System.IO;
using System.Linq;
using WireShelf;

public static class CoreTests
{
    private static int count;
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    public static int Main()
    {
        var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WireShelfTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var a = new ShelfItem { ComponentId = Guid.NewGuid(), Name = "Panel α", SnapshotXml = "<xml>0\n1</xml>", InputIndex = 2 };
            var b = new ShelfItem { ComponentId = Guid.NewGuid(), Name = "Merge", Notes = "Flatten en dos entradas" };
            var lib = new ShelfLibrary(); var section = new ShelfSection { Title = "Árboles" };
            section.Items.Add(a); section.Items.Add(b); lib.Sections.Add(section);
            lib.Sections.Add(new ShelfSection { Title = "Planos" });
            var copy = LibraryStore.Copy(lib);
            Check(copy.Sections.Select(s => s.Title).SequenceEqual(new[] { "Árboles", "Planos" }), "section order + Unicode + empty section roundtrip");
            Check(copy.Sections[0].Items[0].SnapshotXml == a.SnapshotXml && copy.Sections[0].Items[0].InputIndex == 2, "recipe payload and preferred ports roundtrip");
            Check(lib.Search("").First().Items.Select(i => i.Id).SequenceEqual(new[] { a.Id, b.Id }), "unconditional favorites preserve user order");
            Check(lib.Search("árb").First().Items.Count == 2, "search by section title");
            Check(lib.Search("FLATTEN").First().Items.Single().Id == b.Id, "search by notes ignoring case");
            copy.Sections[0].Items.Reverse(); Check(lib.Sections[0].Items[0] == a, "editor draft isolates canceled edits");
            var path = Path.Combine(folder, "library.json"); var store = new LibraryStore(path);
            store.Save(lib); store.Save(copy);
            Check(File.Exists(path + ".bak"), "atomic replacement creates backup");
            File.WriteAllText(path, "{ broken");
            var restored = store.Load();
            Check(store.Recovered && restored.Sections[0].Items[0].Id == a.Id, "corrupt primary recovers last valid backup");
            store.Save(restored);
            Check(LibraryStore.Read(path + ".bak").Sections.Count == 2, "recovery save does not overwrite valid backup with corrupt primary");
            Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "no unfinished temporary writes");
            var bad = LibraryStore.Copy(lib); bad.Version = 2;
            Reject(bad, "unsupported schema rejected");
            bad = LibraryStore.Copy(lib); bad.Sections[0].Items[1].Id = a.Id; Reject(bad, "duplicate IDs rejected");
            bad = LibraryStore.Copy(lib); bad.Sections[0].Items[0].InputIndex = -1; Reject(bad, "invalid port rejected");
            bad = LibraryStore.Copy(lib); bad.Sections[0].Title = " "; Reject(bad, "empty section title rejected");
            lib.CatalogRevision = 1;
            Check(LibraryStore.Copy(lib).CatalogRevision == 1, "catalog migration revision persists");
            var tap = new ShiftTap();
            tap.Press(0); Check(!tap.Release(80, 500), "single Shift does not open palette");
            tap.Press(150); Check(tap.Release(200, 500), "double Shift opens on second release");
            tap.Press(250); Check(!tap.Release(300, 500), "triple tap does not reopen palette");
            tap.Reset(); tap.Press(0); tap.Press(300); Check(!tap.Release(350, 500), "held Shift autorepeat rejected");
            tap.Reset(); tap.Press(0); tap.Release(50, 500); tap.Press(600); Check(!tap.Release(650, 500), "slow taps do not activate");
            tap.Reset(); tap.Press(0); tap.Release(50, 500); tap.Press(100); tap.Reset(); Check(!tap.Release(150, 500), "chord or mouse action cancels pending gesture");
            var many = new ShelfLibrary();
            foreach (var size in new[] { 25, 25, 20, 19, 12, 24, 12, 18, 6, 8, 5, 23 }) {
                var group = new ShelfSection();
                for (var i = 0; i < size; i++) group.Items.Add(new ShelfItem { ComponentId = Guid.NewGuid() });
                many.Sections.Add(group);
            }
            var grid = new ShelfGridLayout(many.Sections, 9, 2);
            Check(grid.Cells.Count == 197 && grid.Size.Width <= 900 && grid.Size.Height <= 800, "all 197 requested icons fit together in a desktop palette");
            Check(grid.Cells.All(c => c.Bounds.Left >= 0 && c.Bounds.Top >= 0 && c.Bounds.Right <= grid.Size.Width && c.Bounds.Bottom <= grid.Size.Height), "grid contains every icon");
            Check(!grid.Cells.SelectMany((c, i) => grid.Cells.Skip(i + 1).Where(other => c.Bounds.IntersectsWith(other.Bounds))).Any(), "icon hit targets never overlap");
            Check(!grid.Cells.Any(c => grid.Headings.Any(h => c.Bounds.IntersectsWith(h.Bounds))), "section headings never overlap icons");
            Console.WriteLine(count + " core checks passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(folder, true); }
    }
    private static void Reject(ShelfLibrary library, string name)
    {
        try { LibraryStore.Encode(library); } catch (InvalidDataException) { Check(true, name); return; }
        throw new Exception(name);
    }
}
