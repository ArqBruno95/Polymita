using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
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
            else if (action == "before") Containers(doc, true);
            else if (action == "after") Containers(doc, false);
            else if (action == "absorb") Absorb(doc);
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
        // Centres an object's laid-out bounds on a canvas point.
        private static void Place(IGH_DocumentObject obj, PointF centre)
        {
            obj.Attributes.Pivot = centre;
            obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout();
            var box = obj.Attributes.Bounds;
            obj.Attributes.Pivot = new PointF(centre.X + centre.X - (box.Left + box.Width/2),
                centre.Y + centre.Y - (box.Top + box.Height/2));
            obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout();
        }
        private static Param_GenericObject Container()
        {
            var param = new Param_GenericObject();
            param.CreateAttributes();
            // Drawn as its name rather than its icon, the way a labelled relay reads.
            param.IconDisplayMode = GH_IconDisplayMode.name;
            param.Attributes.Selected = false;
            return param;
        }
        // Alt+A / Alt+D. Splices a generic Data parameter into every wire on the chosen
        // side of each selected object, the way a relay sits in a wire. A port with no
        // wire gets a container of its own, connected to that port.
        public static List<IGH_DocumentObject> Containers(GH_Document doc, bool before)
        {
            if (doc == null) throw new InvalidOperationException("Open a definition and select a component.");
            var selected = doc.Objects.Where(o => o.Attributes.Selected && (o is IGH_Component || o is IGH_Param)).ToList();
            if (selected.Count == 0) throw new InvalidOperationException("Select one or more components first.");
            var record = new GH_UndoRecord("Polymita · Data " + (before ? "before" : "after"));
            var added = new List<IGH_DocumentObject>();
            var rewired = new List<IGH_Param>();
            // Ports on the side being extended: inputs when inserting before, outputs after.
            var work = new List<Tuple<IGH_Param,IGH_Param,Param_GenericObject>>();
            foreach (var obj in selected)
                foreach (var port in Recipes.Ports(obj, !before))
                {
                    if (port.Attributes == null) continue;
                    var partners = (before ? port.Sources : port.Recipients).ToArray();
                    if (partners.Length == 0)
                    {
                        var grip = before ? port.Attributes.InputGrip : port.Attributes.OutputGrip;
                        var container = Container();
                        Place(container, new PointF(grip.X + (before ? -90 : 90), grip.Y));
                        work.Add(Tuple.Create(before ? (IGH_Param)null : port, before ? port : (IGH_Param)null, container));
                        added.Add(container);
                        continue;
                    }
                    for (var i = 0; i < partners.Length; i++)
                    {
                        var upstream = before ? partners[i] : port;
                        var downstream = before ? port : partners[i];
                        if (upstream.Attributes == null || downstream.Attributes == null) continue;
                        var from = upstream.Attributes.OutputGrip; var to = downstream.Attributes.InputGrip;
                        var container = Container();
                        Place(container, new PointF((from.X + to.X)/2, (from.Y + to.Y)/2));
                        work.Add(Tuple.Create(upstream, downstream, container));
                        added.Add(container);
                        if (!rewired.Contains(downstream)) rewired.Add(downstream);
                    }
                }
            if (work.Count == 0) throw new InvalidOperationException("The selection has no ports on that side.");
            foreach (var input in rewired) record.AddAction(new GH_WireAction(input));
            var placed = new List<IGH_DocumentObject>();
            try
            {
                foreach (var job in work)
                {
                    doc.AddObject(job.Item3, false); placed.Add(job.Item3);
                    record.AddAction(new GH_AddObjectAction(job.Item3));
                    if (job.Item1 != null) job.Item3.AddSource(job.Item1);
                    if (job.Item2 != null)
                    {
                        if (job.Item1 != null) job.Item2.RemoveSource(job.Item1);
                        job.Item2.AddSource(job.Item3);
                    }
                }
                doc.UndoUtil.RecordEvent(record);
            }
            catch { doc.RemoveObjects(placed.ToArray(), false); throw; }
            foreach (var job in work) { job.Item3.ExpireSolution(false); if (job.Item2 != null) job.Item2.ExpireSolution(false); }
            doc.NewSolution(false); return added;
        }
        // Alt+G. Every object that overlaps a selected group joins it.
        public static int Absorb(GH_Document doc)
        {
            if (doc == null) throw new InvalidOperationException("Open a definition first.");
            // With nothing selected every group takes in what overlaps it, which is the
            // tidy-the-whole-definition case; a selection narrows it to those groups.
            var groups = doc.Objects.OfType<GH_Group>().Where(g => g.Attributes.Selected).ToList();
            if (groups.Count == 0) groups = doc.Objects.OfType<GH_Group>().ToList();
            if (groups.Count == 0) throw new InvalidOperationException("This definition has no groups.");
            var record = new GH_UndoRecord("Polymita · Add overlapping objects to group");
            foreach (var group in groups) record.AddAction(new GH_GenericObjectAction(group));
            var count = 0;
            foreach (var group in groups)
            {
                var box = group.Attributes.Bounds;
                var held = new HashSet<Guid>(group.ObjectIDs);
                // A group that already contains this one cannot become its member.
                var ancestors = new HashSet<Guid>();
                bool grew;
                ancestors.Add(group.InstanceGuid);
                do { grew=false; foreach (var other in doc.Objects.OfType<GH_Group>())
                    if (other.ObjectIDs.Any(ancestors.Contains) && ancestors.Add(other.InstanceGuid)) grew=true; } while (grew);
                foreach (var obj in doc.Objects.ToArray())
                {
                    if (ancestors.Contains(obj.InstanceGuid) || held.Contains(obj.InstanceGuid)) continue;
                    if (!(obj is IGH_Component || obj is IGH_Param || obj is GH_Group)) continue;
                    if (obj.Attributes == null || !obj.Attributes.Bounds.IntersectsWith(box)) continue;
                    group.AddObject(obj.InstanceGuid); held.Add(obj.InstanceGuid); count++;
                }
                group.ExpireCaches(); group.Attributes.ExpireLayout(); group.Attributes.PerformLayout();
            }
            if (count > 0) doc.UndoUtil.RecordEvent(record);
            return count;
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
                // Straight beside the original, overlapping whatever is already there.
                // Hunting for clear space threw the copy far from what it was copied from.
                var offset = bounds.Width + 25;
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

