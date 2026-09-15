using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Undo;
using Grasshopper.Kernel.Undo.Actions;
using System;
using System.Drawing;
using System.Linq;

namespace Polymita
{
    public static class Insertion
    {
        // A capsule draws each port's nickname, and only swaps to the full name when the
        // machine-wide Draw Full Names setting is on. Toggling that setting from here
        // changed every other object on the canvas and still did not repaint reliably,
        // so instead each inserted port is named after itself: the full name shows
        // whichever way the setting is left, and nothing else on the canvas moves.
        public static void FullPortNames(IGH_DocumentObject obj)
        {
            var component = obj as IGH_Component;
            if (component == null) return;
            foreach (var port in component.Params.Input)
                if (!String.IsNullOrEmpty(port.Name)) port.NickName = port.Name;
            foreach (var port in component.Params.Output)
                if (!String.IsNullOrEmpty(port.Name)) port.NickName = port.Name;
            obj.Attributes.ExpireLayout(); obj.Attributes.PerformLayout();
        }
        public static IGH_DocumentObject Insert(GH_Document document, ShelfItem item, PointF position,
            IGH_Param anchor, bool fromInput, int portIndex)
        {
            if (document == null) throw new InvalidOperationException("Open a Grasshopper definition.");
            if (anchor != null && !document.Objects.Contains(anchor.Attributes.GetTopLevel.DocObject))
                throw new InvalidOperationException("The source component is no longer in this definition.");
            var obj = Recipes.Create(item);
            FullPortNames(obj);
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

