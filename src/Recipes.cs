using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Parameters;
using GH_IO.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Polymita
{
    public static class Recipes
    {
        public static IGH_DocumentObject Create(ShelfItem item)
        {
            var proxy = Instances.ComponentServer.EmitObjectProxy(item.ComponentId);
            if (proxy == null) throw new InvalidOperationException("Cannot find the installed component for «" + item.Name + "».");
            var obj = proxy.CreateInstance();
            if (obj == null) throw new InvalidOperationException("Could not create «" + item.Name + "».");
            obj.CreateAttributes();
            if (item.IsRecipe)
            {
                ValidateXml(item.SnapshotXml);
                var chunk = new GH_LooseChunk("Polymita");
                chunk.Deserialize_Xml(item.SnapshotXml);
                if (!obj.Read(chunk)) throw new InvalidDataException("Grasshopper could not restore this recipe.");
            }
            DetachAndRenew(obj);
            obj.Attributes.Selected = false;
            obj.Attributes.ExpireLayout();
            obj.Attributes.PerformLayout();
            return obj;
        }

        public static ShelfItem Capture(IGH_DocumentObject original)
        {
            if (!(original is IGH_Component) && !(original is IGH_Param))
                throw new InvalidOperationException("Select a component or parameter, such as Panel or Merge.");
            var item = new ShelfItem { ComponentId = original.ComponentGuid, Name = original.NickName,
                Notes = original.Name + " · saved configuration" };
            var chunk = new GH_LooseChunk("Polymita");
            if (!original.Write(chunk)) throw new InvalidOperationException("This component cannot save its state.");
            item.SnapshotXml = chunk.Serialize_Xml();
            // Work on a detached copy. Never disconnect or mutate the source document.
            var copy = Create(item);
            copy.Attributes.Pivot = System.Drawing.PointF.Empty;
            var clean = new GH_LooseChunk("Polymita");
            if (!copy.Write(clean)) throw new InvalidOperationException("The component copy could not be saved.");
            item.SnapshotXml = clean.Serialize_Xml();
            return item;
        }

        private static void ValidateXml(string xml)
        {
            if (xml.Length > 8 * 1024 * 1024) throw new InvalidDataException("The recipe exceeds 8 MB.");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 8 * 1024 * 1024 };
            using (var reader = XmlReader.Create(new StringReader(xml), settings)) while (reader.Read()) { }
        }

        private static void DetachAndRenew(IGH_DocumentObject obj)
        {
            obj.NewInstanceGuid();
            var component = obj as IGH_Component;
            if (component != null)
            {
                foreach (var input in component.Params.Input) { input.RemoveAllSources(); input.NewInstanceGuid(); }
                foreach (var output in component.Params.Output) output.NewInstanceGuid();
            }
            else
            {
                var param = obj as IGH_Param;
                if (param != null) param.RemoveAllSources();
            }
        }

        // Port availability depends only on direction, never on the type/value of the existing wire.
        public static IList<IGH_Param> Ports(IGH_DocumentObject obj, bool fromInput)
        {
            var component = obj as IGH_Component;
            if (component != null) return fromInput ? component.Params.Output : component.Params.Input;
            var param = obj as IGH_Param;
            if (param != null && (fromInput ? param.Attributes.HasOutputGrip : param.Attributes.HasInputGrip))
                return new List<IGH_Param> { param };
            return new List<IGH_Param>();
        }

        public static ShelfLibrary Defaults()
        {
            var library = new ShelfLibrary();
            var data = new ShelfSection { Title = "Data and trees" };
            var panel = new GH_Panel(); panel.CreateAttributes(); panel.UserText = "0\n0.25\n0.5\n0.75\n1";
            var preset = Capture(panel); preset.Name = "Panel · list 0–1"; data.Items.Add(preset);
            Add(data, "Panel", "Panel");
            Add(data, "Merge", "Merge · Flatten", obj => {
                var c = obj as IGH_Component;
                if (c != null) foreach (var p in c.Params.Input) p.DataMapping = GH_DataMapping.Flatten;
            });
            Add(data, "Shift Paths", "Shift Paths · Simplify", SimplifyFirst);
            if (!data.Items.Any(x => x.Name == "Shift Paths · Simplify")) Add(data, "Shift Path", "Shift Path · Simplify", SimplifyFirst);
            Add(data, "List Item", "List Item"); Add(data, "Flatten Tree", "Flatten Tree");
            library.Sections.Add(data);
            var planes = new ShelfSection { Title = "Planes" };
            Add(planes, "XY Plane", "XY Plane"); Add(planes, "Construct Plane", "Construct Plane"); library.Sections.Add(planes);
            var points = new ShelfSection { Title = "Points" };
            points.Items.Add(new ShelfItem { ComponentId = new Param_Point().ComponentGuid, Name = "Point" });
            Add(points, "Construct Point", "Construct Point"); Add(points, "Deconstruct Point", "Deconstruct Point"); library.Sections.Add(points);
            var curves = new ShelfSection { Title = "Curves" };
            curves.Items.Add(new ShelfItem { ComponentId = new Param_Curve().ComponentGuid, Name = "Curve" });
            Add(curves, "Join Curves", "Join Curves"); Add(curves, "Divide Curve", "Divide Curve"); library.Sections.Add(curves);
            return library;
        }
        private static void SimplifyFirst(IGH_DocumentObject obj)
        {
            var c = obj as IGH_Component;
            if (c != null && c.Params.Input.Count > 0) c.Params.Input[0].Simplify = true;
        }
        private static void Add(ShelfSection section, string name, string label, Action<IGH_DocumentObject> setup = null)
        {
            var proxy = Instances.ComponentServer.ObjectProxies.FirstOrDefault(p => !p.Obsolete && p.Desc.Name == name &&
                (p.Desc.Category == "Params" || p.Desc.Category == "Sets" || p.Desc.Category == "Vector" || p.Desc.Category == "Curve"));
            if (proxy == null) return;
            var item = new ShelfItem { ComponentId = proxy.Guid, Name = label };
            if (setup != null)
            { var obj = proxy.CreateInstance(); obj.CreateAttributes(); setup(obj); item = Capture(obj); item.Name = label; }
            section.Items.Add(item);
        }
    }
}

