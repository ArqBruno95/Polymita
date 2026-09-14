using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Undo;
using Grasshopper.Kernel.Undo.Actions;
using System;
using System.Drawing;
using System.Linq;

namespace WireShelf
{
    public static class Insertion
    {
        // Grasshopper keeps full port names as one canvas-wide display setting, so a
        // favorite cannot carry it on its own. Setting the flag is not enough either:
        // laid-out capsules keep the caption widths they were built with, so every
        // object has to be told to lay itself out again.
        public static void FullNames(GH_Document document)
        {
            if (!Grasshopper.CentralSettings.CanvasFullNames)
            {
                Grasshopper.CentralSettings.CanvasFullNames = true;
                if (document != null)
                    foreach (var existing in document.Objects)
                        if (existing.Attributes != null) { existing.Attributes.ExpireLayout(); existing.Attributes.PerformLayout(); }
                if (Grasshopper.Instances.ActiveCanvas != null) Grasshopper.Instances.ActiveCanvas.Invalidate();
            }
        }
        public static IGH_DocumentObject Insert(GH_Document document, ShelfItem item, PointF position,
            IGH_Param anchor, bool fromInput, int portIndex)
        {
            if (document == null) throw new InvalidOperationException("Open a Grasshopper definition.");
            if (anchor != null && !document.Objects.Contains(anchor.Attributes.GetTopLevel.DocObject))
                throw new InvalidOperationException("The source component is no longer in this definition.");
            FullNames(document);
            var obj = Recipes.Create(item);
            var ports = Recipes.Ports(obj, fromInput);
            if (anchor != null && (portIndex < 0 || portIndex >= ports.Count))
                throw new InvalidOperationException("This favorite has no such port. Choose another port or insert without a wire.");
            obj.Attributes.Pivot = position;
            obj.Attributes.ExpireLayout();
            obj.Attributes.PerformLayout();
            if (anchor != null)
            {
                var grip = fromInput ? ports[portIndex].Attributes.OutputGrip : ports[portIndex].Attributes.InputGrip;
                obj.Attributes.Pivot = new PointF(position.X + position.X - grip.X, position.Y + position.Y - grip.Y);
                obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout();
            }
            var record = new GH_UndoRecord("Polymita · " + item.Name);
            IGH_Param recipient = anchor == null ? null : (fromInput ? anchor : ports[portIndex]);
            var wire = recipient == null ? null : new GH_WireAction(recipient);
            var added = false;
            try
            {
                document.AddObject(obj, false); added = true;
                record.AddAction(new GH_AddObjectAction(obj));
                if (wire != null) record.AddAction(wire);
                if (recipient != null) recipient.AddSource(fromInput ? ports[portIndex] : anchor);
                document.UndoUtil.RecordEvent(record);
            }
            catch
            {
                if (recipient != null) recipient.RemoveSource(fromInput ? ports[portIndex] : anchor);
                if (added) document.RemoveObject(obj, false);
                throw;
            }
            var active = obj as IGH_ActiveObject;
            if (active != null) active.ExpireSolution(false);
            if (recipient != null) recipient.ExpireSolution(false);
            document.NewSolution(false);
            return obj;
        }
    }
}

