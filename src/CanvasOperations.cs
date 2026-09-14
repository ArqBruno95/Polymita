using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Undo;
using Grasshopper.Kernel.Undo.Actions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WireShelf
{
    public static class CanvasOperations
    {
        public static void Run(GH_Document doc, string action)
        {
            if (doc == null) throw new InvalidOperationException("Open a definition and select the components.");
            if (action == "connect") Connect(doc);
            else if (action == "duplicate") Duplicate(doc);
            else throw new InvalidOperationException("Unknown operation.");
        }
        public static int Connect(GH_Document doc)
        {
            var selected = doc.Objects.Where(o => o.Attributes.Selected && (o is IGH_Component || o is IGH_Param))
                .OrderBy(o => o.Attributes.Pivot.X).ThenBy(o => o.Attributes.Pivot.Y).ToList();
            if (selected.Count < 2) throw new InvalidOperationException("Select two or more components. Place the receiver on the right.");
            var target = selected.Last();
            if (selected[selected.Count-2].Attributes.Pivot.X == target.Attributes.Pivot.X)
                throw new InvalidOperationException("Move the receiver a little farther right to distinguish it.");
            var inputs = Recipes.Ports(target,false);
            var outputs = selected.Take(selected.Count-1).SelectMany(o => Recipes.Ports(o,true)).ToList();
            if (inputs.Count == 0 || outputs.Count == 0) throw new InvalidOperationException("The selection has no connectable outputs and inputs.");
            var downstream = doc.FindAllDownstreamObjects((IGH_ActiveObject)target);
            if (selected.Take(selected.Count-1).Any(o => downstream.Contains((IGH_ActiveObject)o)))
                throw new InvalidOperationException("This connection would create a cycle. Check the component direction.");
            var pairs = new List<Tuple<IGH_Param,IGH_Param>>();
            for (int i=0; i<outputs.Count && (inputs.Count == 1 || i<inputs.Count); i++)
            {
                var input = inputs[inputs.Count == 1 ? 0 : i];
                if (!input.Sources.Contains(outputs[i])) pairs.Add(Tuple.Create(outputs[i],input));
            }
            if (pairs.Count == 0) return 0;
            var record = new GH_UndoRecord("Polymita · Connect selection");
            foreach (var input in pairs.Select(p=>p.Item2).Distinct()) record.AddAction(new GH_WireAction(input));
            var applied = new List<Tuple<IGH_Param,IGH_Param>>();
            try {
                foreach (var pair in pairs) { pair.Item2.AddSource(pair.Item1); applied.Add(pair); }
                doc.UndoUtil.RecordEvent(record);
            }
            catch { foreach (var pair in applied) pair.Item2.RemoveSource(pair.Item1); throw; }
            foreach (var input in pairs.Select(p=>p.Item2).Distinct()) input.ExpireSolution(false);
            doc.NewSolution(false); return pairs.Count;
        }
        public static HashSet<Guid> ExpandSelection(GH_Document doc)
        {
            var ids = new HashSet<Guid>(doc.Objects.Where(o=>o.Attributes.Selected).Select(o=>o.InstanceGuid));
            var pending = new Queue<Guid>(ids);
            while (pending.Count > 0)
            {
                var group = doc.FindObject(pending.Dequeue(),false) as GH_Group;
                if (group == null) continue;
                foreach (var id in group.ObjectIDs) if (ids.Add(id)) pending.Enqueue(id);
            }
            return ids;
        }
        public static float FreeOffset(RectangleF source, IEnumerable<RectangleF> obstacles, float gap)
        {
            var items = obstacles.ToArray(); var dx = source.Width + gap;
            for (int i=0; i<=items.Length; i++)
            {
                var moved = source; moved.Offset(dx,0); moved.Inflate(gap/2,gap/2);
                var hits = items.Where(r=>r.IntersectsWith(moved)).ToArray();
                if (hits.Length == 0) return dx;
                dx = Math.Max(dx + gap, hits.Max(r=>r.Right) + gap - source.Left);
            }
            return Math.Max(dx, items.Max(r=>r.Right) + gap - source.Left);
        }
        public static List<IGH_DocumentObject> Duplicate(GH_Document doc)
        {
            var ids = ExpandSelection(doc);
            if (ids.Count == 0) throw new InvalidOperationException("Select the components or groups to duplicate.");
            // Native document serialization preserves group membership, variable ports and plugin-specific state.
            using (var copy = GH_Document.DuplicateDocument(doc))
            {
                if (copy == null) throw new InvalidOperationException("Grasshopper could not copy the selection.");
                if (!ids.All(id=>copy.FindObject(id,false) != null)) throw new InvalidOperationException("The full selection could not be identified in the copy.");
                var external = new List<Tuple<IGH_Param,IGH_Param>>();
                foreach (var obj in doc.Objects.Where(o=>ids.Contains(o.InstanceGuid)))
                {
                    var clone = copy.FindObject(obj.InstanceGuid,false);
                    var originalInputs = Recipes.Ports(obj,false); var copyInputs = Recipes.Ports(clone,false);
                    for (int i=0; i<originalInputs.Count; i++)
                        foreach (var source in originalInputs[i].Sources)
                            if (!ids.Contains(source.Attributes.GetTopLevel.DocObject.InstanceGuid)) external.Add(Tuple.Create(copyInputs[i],source));
                }
                copy.RemoveObjects(copy.Objects.Where(o=>!ids.Contains(o.InstanceGuid)).ToArray(),false);
                foreach (var obj in copy.Objects) { obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout(); }
                var bounds = copy.BoundingBox(false);
                var offset = FreeOffset(bounds, doc.Objects.Select(o=>o.Attributes.Bounds), 70);
                copy.MutateAllIds();
                var clones = copy.Objects.ToList();
                foreach (var obj in clones.Where(o=>!(o is GH_Group)))
                {
                    obj.Attributes.Pivot = new PointF(obj.Attributes.Pivot.X+offset,obj.Attributes.Pivot.Y);
                    obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout();
                }
                foreach (var obj in clones.OfType<GH_Group>()) { obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout(); }
                var undo = new GH_UndoRecord("Polymita · Duplicate selection");
                try
                {
                    if (!doc.MergeDocument(copy,false)) throw new InvalidOperationException("The copy could not be inserted.");
                    foreach (var wire in external) wire.Item1.AddSource(wire.Item2);
                    foreach (var obj in clones) undo.AddAction(new GH_AddObjectAction(obj));
                    doc.UndoUtil.RecordEvent(undo);
                }
                catch { doc.RemoveObjects(clones.Where(o=>doc.Objects.Contains(o)).ToArray(),false); throw; }
                doc.DeselectAll();
                foreach (var obj in clones) { obj.Attributes.Selected = true; var active = obj as IGH_ActiveObject; if(active != null) active.ExpireSolution(false); }
                doc.NewSolution(false); return clones;
            }
        }
    }
}

