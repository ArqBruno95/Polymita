using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using WireShelf;

public static class IntegrationTests
{
    private static TextWriter log;
    private static int count;
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); count++; log.WriteLine("PASS " + name); log.Flush(); }
    public static void Run(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "test-output"));
        using (log = new StreamWriter(Path.Combine(root, "test-output", "integration.txt")))
        {
            try
            {
                log.WriteLine("Rhino " + Rhino.RhinoApp.Version + " | Runtime " + Environment.Version); log.Flush();
                var panel = new GH_Panel(); panel.CreateAttributes(); panel.UserText = "rojo\nverde\nazul";
                panel.NickName = "Materiales"; panel.Attributes.Pivot = new PointF(100, 120);
                var recipe = Recipes.Capture(panel);
                var first = (GH_Panel)Recipes.Create(recipe); var second = (GH_Panel)Recipes.Create(recipe);
                Check(first.UserText.Replace("\r", "") == panel.UserText.Replace("\r", "") && first.NickName == panel.NickName, "Panel text and nickname preserved (native CRLF normalization)");
                Check(first.InstanceGuid != second.InstanceGuid && first.InstanceGuid != panel.InstanceGuid, "instances have unique IDs");
                Check(panel.Attributes.Pivot == new PointF(100, 120), "capture leaves original position untouched");
                var lib = Recipes.Defaults();
                var items = lib.Sections.SelectMany(s => s.Items).ToList();
                log.WriteLine("Defaults: " + String.Join(", ", items.Select(i => i.Name))); log.Flush();
                var mergeItem = items.Single(i => i.Name == "Merge · Flatten");
                var merge = (IGH_Component)Recipes.Create(mergeItem);
                Check(merge.Params.Input.Count >= 2 && merge.Params.Input.All(p => p.DataMapping == GH_DataMapping.Flatten), "Merge retains Flatten on all inputs");
                var shift = (IGH_Component)Recipes.Create(items.Single(i => i.Name.Contains("Simplify")));
                Check(shift.Params.Input[0].Simplify, "Shift Paths retains Simplify on data input");
                var capturedMerge = Recipes.Capture(merge);
                var merge2 = (IGH_Component)Recipes.Create(capturedMerge);
                Check(!merge.Params.Input.Select(p => p.InstanceGuid).Intersect(merge2.Params.Input.Select(p => p.InstanceGuid)).Any(), "component ports receive fresh IDs");
                using (var doc = new GH_Document())
                {
                    doc.AddObject(panel, false);
                    var wired = (IGH_Component)Insertion.Insert(doc, mergeItem, new PointF(340, 180), panel, false, 1);
                    var wiredId = wired.InstanceGuid;
                    Check(wired.Params.Input[1].Sources.Contains(panel) && wired.Params.Input[0].Sources.Count == 0, "output drag connects chosen input (second Merge port)");
                    Check(wired.Params.Input.Take(2).All(p => p.DataMapping == GH_DataMapping.Flatten), "insertion retains Flatten on both saved inputs (GH may append a spare port)");
                    Check(doc.UndoServer.UndoCount == 1, "creation and connection use one undo record");
                    doc.Undo(); Check(doc.Objects.Count == 1 && panel.Recipients.Count == 0, "single Undo removes component and wire");
                    doc.Redo();
                    var redone = (IGH_Component)doc.FindObject(wiredId, false);
                    Check(redone != null && redone.Params.Input[1].Sources.Contains(panel), "Redo restores component and connection");
                    var existing = redone.Params.Input[1]; var before = existing.Sources.Count;
                    var inserted = Insertion.Insert(doc, recipe, new PointF(80, 310), existing, true, 0);
                    Check(existing.Sources.Count == before + 1 && existing.Sources.Contains((IGH_Param)inserted), "input drag adds new source while preserving existing wires");
                    doc.Undo(); Check(existing.Sources.Count == before && existing.Sources.Contains(panel), "Undo upstream insertion preserves original sources");
                    doc.Redo(); Check(existing.Sources.Count == before + 1, "Redo upstream insertion restores added source");
                    var snap = Recipes.Capture(redone);
                    var detached = (IGH_Component)Recipes.Create(snap);
                    Check(detached.Params.Input.All(p => p.SourceCount == 0) && existing.SourceCount == before + 1, "capture strips external wires only from copy");
                    var ids = doc.Objects.Select(o => o.InstanceGuid).ToArray();
                    try { Insertion.Insert(doc, mergeItem, PointF.Empty, panel, false, 999); throw new Exception("invalid port accepted"); }
                    catch (InvalidOperationException) { }
                    Check(ids.SequenceEqual(doc.Objects.Select(o => o.InstanceGuid)), "invalid port leaves document unchanged");
                    var curve = new Param_Curve(); curve.CreateAttributes(); doc.AddObject(curve, false);
                    var independent = Insertion.Insert(doc, recipe, new PointF(500, 400), curve, false, 0);
                    Check(independent != null, "data-type mismatch never filters or blocks favorite creation");
                }
                Check(lib.Search("").SelectMany(s => s.Items).Count() == items.Count, "palette exposes the entire library without data context");
                var previous = items.Select(i => i.Id).ToArray();
                Check(BuiltInCatalog.Apply(lib), "catalog expands legacy library");
                var expanded = lib.Sections.SelectMany(s => s.Items).ToList();
                Check(previous.All(id => expanded.Any(i => i.Id == id)), "migration preserves all existing favorites and recipes");
                Check(expanded.Count >= 197 && expanded.All(i => Instances.ComponentServer.EmitObjectProxy(i.ComponentId) != null), "entire requested catalog resolves to installed components");
                Check(!BuiltInCatalog.Apply(lib), "catalog migration is idempotent");
                var deleted = lib.Sections.Last().Items.Last(); lib.Sections.Last().Items.Remove(deleted);
                BuiltInCatalog.Apply(lib);
                Check(!lib.Sections.SelectMany(s => s.Items).Any(i => i.Id == deleted.Id), "deleted favorites stay deleted after migration");
                Check(((GH_Panel)Recipes.Create(expanded.Single(i => i.Name == "Panel · 0"))).UserText == "0" &&
                    ((GH_Panel)Recipes.Create(expanded.Single(i => i.Name == "Panel · 0-1"))).UserText == "0-1", "both original QuickConnection panel presets preserved");
                using (var detachedDoc = new GH_Document()) {
                    var standalone = (IGH_Component)Insertion.Insert(detachedDoc, mergeItem, new PointF(60, 90), null, false, 0);
                    Check(standalone.Params.Input.All(p => p.SourceCount == 0), "standalone insertion has no wires");
                    detachedDoc.Undo(); Check(detachedDoc.Objects.Count == 0, "standalone insertion supports one-step Undo");
                }
                log.WriteLine(count + " integration checks passed.");
                // Keep the user's active definition and edits in place during reruns.
                if (Instances.ActiveCanvas.Document != null) return;
                var fixture = new GH_Document();
                var demoPanel = new GH_Panel(); demoPanel.CreateAttributes(); demoPanel.UserText = "0\n0.25\n0.5\n0.75\n1";
                demoPanel.Attributes.Pivot = new PointF(150, 180); fixture.AddObject(demoPanel, false);
                var demoPoint = new Param_Point(); demoPoint.CreateAttributes(); demoPoint.Attributes.Pivot = new PointF(150, 365); fixture.AddObject(demoPoint, false);
                Instances.DocumentServer.AddDocument(fixture); Instances.ActiveCanvas.Document = fixture;
                Instances.ActiveCanvas.Refresh();
            }
            catch (Exception ex) { log.WriteLine("FAIL " + ex); throw; }
        }
    }
}
