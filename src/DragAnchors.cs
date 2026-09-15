using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Polymita
{
    // Built once at mouse-down. Each local grip retains only its actual external
    // wire endpoints; cursor distance is measured before any snapping takes place.
    internal sealed class DragAnchors
    {
        internal readonly PointF[] Targets;
        internal readonly float AnchorY;
        internal readonly IGH_Param LocalPort;
        internal readonly bool IsOutput;

        internal static IGH_DocumentObject Reference(GH_Document doc, HashSet<Guid> moving, PointF cursor)
        {
            var hit=doc.FindAttribute(cursor,true);
            var pressed=hit==null ? null : hit.GetTopLevel.DocObject;
            return pressed!=null && moving.Contains(pressed.InstanceGuid) ? pressed : null;
        }

        internal DragAnchors(GH_Document doc, HashSet<Guid> moving,
            IList<IGH_DocumentObject> objects, HashSet<Guid> excluded, PointF cursor, RectangleF bounds)
        {
            AnchorY=bounds.Top+bounds.Height/2;
            var pressed=Reference(doc,moving,cursor);
            // Grabbing a group aligns its outline and centre, without an arbitrary
            // member's stronger wire snap overriding the group guide.
            if (pressed is GH_Group) { Targets=new PointF[0]; return; }
            // Use only the grabbed object's ports, even in a multiple selection.
            var candidates=pressed!=null ? new[]{pressed} : objects;
            float closest=float.MaxValue;
            PointF[] chosen=null;
            foreach (var obj in candidates)
                foreach (var output in new[]{false,true})
                    foreach (var port in Recipes.Ports(obj, output))
                    {
                        var attributes=port.Attributes;
                        if (attributes==null || !(output ? attributes.HasOutputGrip : attributes.HasInputGrip)) continue;
                        var external=new List<PointF>();
                        foreach (var remote in output ? port.Recipients : port.Sources)
                        {
                            var other=remote.Attributes;
                            if (other==null || moving.Contains(other.GetTopLevel.DocObject.InstanceGuid)) continue;
                            if (output ? other.HasInputGrip : other.HasOutputGrip)
                                external.Add(output ? other.InputGrip : other.OutputGrip);
                        }
                        // An unused port has no wire to choose and cannot hijack the
                        // nearest connected wire, even when its grip is closer.
                        if (external.Count==0) continue;
                        var grip=output ? attributes.OutputGrip : attributes.InputGrip;
                        float dx=grip.X-cursor.X, dy=grip.Y-cursor.Y;
                        float distance=dx*dx+dy*dy;
                        if (!(distance<closest)) continue;
                        closest=distance; AnchorY=grip.Y; LocalPort=port; IsOutput=output;
                        chosen=external.Distinct().ToArray();
                    }
            if (chosen!=null) { Targets=chosen; return; }

            // No external wire at the grabbed object: its centre axes can still
            // align to the real input/output vertices of stationary components.
            var stationary=new List<PointF>();
            foreach (var obj in doc.Objects.Where(o=>!excluded.Contains(o.InstanceGuid)))
                foreach (var output in new[]{false,true})
                    foreach (var port in Recipes.Ports(obj, output))
                    {
                        var attributes=port.Attributes;
                        if (attributes!=null && (output ? attributes.HasOutputGrip : attributes.HasInputGrip))
                            stationary.Add(output ? attributes.OutputGrip : attributes.InputGrip);
                    }
            Targets=stationary.Distinct().ToArray();
        }
    }
}
