using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using WireShelf;
public static class OperationsTests {
 static TextWriter log; static int count;
 static void Check(bool value,string name) { if(!value) throw new Exception(name); count++; log.WriteLine("PASS "+name); log.Flush(); }
 static T Add<T>(GH_Document doc,T obj,float x,float y) where T:IGH_DocumentObject { obj.CreateAttributes(); obj.Attributes.Pivot=new PointF(x,y); doc.AddObject(obj,false); obj.Attributes.PerformLayout(); return obj; }
 public static void Run(string root) {
  count=0; var canvas=Instances.ActiveCanvas; var original=canvas.Document;
  using(log=new StreamWriter(Path.Combine(root,"test-output","operations.txt"))) try {
   using(var doc=new GH_Document()) {
    canvas.Document=doc;
    var a=Add(doc,new GH_Panel(),100,100); a.UserText="1\n2\n3";
    var b=Add(doc,new GH_Panel(),100,300); b.UserText="4";
    var target=Add(doc,new Param_Number(),450,100);
    a.Attributes.Selected=b.Attributes.Selected=target.Attributes.Selected=true;
    Check(CanvasOperations.Connect(doc)==2 && target.SourceCount==2,"Multiple left sources connect to single right input");
    Check(CanvasOperations.Connect(doc)==0,"Existing connections are not duplicated");
    doc.Undo(); Check(target.SourceCount==0,"Connect has one-step Undo");
    doc.Redo(); Check(target.SourceCount==2,"Connect Redo restores both wires");
    var cycle=Add(doc,new Param_Number(),700,300); cycle.AddSource(target);
    doc.DeselectAll(); target.Attributes.Selected=cycle.Attributes.Selected=true;
    target.Attributes.Pivot=new PointF(900,100);
    bool rejected=false; try { CanvasOperations.Connect(doc); } catch(InvalidOperationException) { rejected=true; }
    Check(rejected && target.SourceCount==2,"Cycle rejected without changing wires");
   }
   using(var doc=new GH_Document()) {
    canvas.Document=doc;
    var external=Add(doc,new GH_Panel(),-100,100); external.UserText="7";
    var a=Add(doc,new GH_Panel(),100,100); a.UserText="red\ngreen\nblue"; a.NickName="Palette";
    var b=Add(doc,new Param_GenericObject(),300,100); b.DataMapping=GH_DataMapping.Flatten; b.Simplify=true; b.AddSource(a); b.AddSource(external);
    var inner=Add(doc,new GH_Group(),0,0); inner.NickName="Inner"; inner.Colour=Color.Coral; inner.AddObject(a.InstanceGuid); inner.AddObject(b.InstanceGuid);
    var outer=Add(doc,new GH_Group(),0,0); outer.NickName="Outer"; outer.AddObject(inner.InstanceGuid);
    var obstacle=Add(doc,new GH_Panel(),700,100);
    foreach(var o in doc.Objects) { o.Attributes.ExpireLayout(); o.Attributes.PerformLayout(); }
    var originalBounds=doc.Objects.Select(o=>o.Attributes.Bounds).ToArray();
    doc.DeselectAll(); outer.Attributes.Selected=true;
    Check(CanvasOperations.ExpandSelection(doc).Count==4,"Selected group expands recursively");
    var clones=CanvasOperations.Duplicate(doc);
    Check(clones.Count==4 && doc.Objects.Count==10,"Copies only selection and nested group contents");
    var ca=clones.OfType<GH_Panel>().Single(); var cb=clones.OfType<Param_GenericObject>().Single();
    Check(ca.UserText.Replace("\r","")==a.UserText.Replace("\r","") && ca.NickName==a.NickName,"Copy preserves panel contents and nickname");
    Check(cb.DataMapping==GH_DataMapping.Flatten && cb.Simplify,"Copy preserves Flatten and Simplify");
    Check(cb.Sources.Contains(ca) && !cb.Sources.Contains(a),"Internal wire points to copied source");
    Check(cb.Sources.Contains(external),"External input remains connected to original source");
    var ci=clones.OfType<GH_Group>().Single(g=>g.NickName=="Inner"); var co=clones.OfType<GH_Group>().Single(g=>g.NickName=="Outer");
    Check(ci.ObjectIDs.Contains(ca.InstanceGuid) && ci.ObjectIDs.Contains(cb.InstanceGuid) && co.ObjectIDs.Contains(ci.InstanceGuid),"Nested groups reference copied members");
    Check(ci.Colour.ToArgb()==inner.Colour.ToArgb(),"Group color preserved");
    Check(clones.All(o=>o.Attributes.Selected) && !a.Attributes.Selected,"Only copies are selected");
    Check(ca.Attributes.Pivot.X>a.Attributes.Pivot.X && ca.Attributes.Pivot.Y==a.Attributes.Pivot.Y,"Copies keep vertical position and move right");
    Check(clones.All(o=>originalBounds.All(r=>!r.IntersectsWith(o.Attributes.Bounds))),"Copy does not overlap any existing bounds");
    var cloneIds=clones.Select(o=>o.InstanceGuid).ToArray();
    doc.Undo(); Check(doc.Objects.Count==6 && cloneIds.All(id=>doc.FindObject(id,false)==null),"One Undo removes entire duplicate");
    doc.Redo(); Check(doc.Objects.Count==10 && cloneIds.All(id=>doc.FindObject(id,false)!=null),"Redo restores entire duplicate");
    cb=(Param_GenericObject)doc.FindObject(cb.InstanceGuid,false);
    Check(cb.SourceCount==2 && cb.Sources.Contains(external),"Redo preserves internal and external input wires");
   }
   var lib=new ShelfLibrary(); var section=new ShelfSection(); lib.Sections.Add(section); section.Items.Add(new ShelfItem { ActionId="duplicate",Name="Copy" });
   Check(LibraryStore.Copy(lib).Sections[0].Items[0].ActionId=="duplicate","Operation favorite round-trip");
   section.Items[0].ActionId="bad"; bool invalid=false; try { LibraryStore.Encode(lib); } catch(InvalidDataException) { invalid=true; }
   Check(invalid,"Unknown operations rejected");
   log.WriteLine(count+" native operation checks passed.");
  } catch(Exception ex) { log.WriteLine(ex); throw; } finally { canvas.Document=original; }
 }
}
