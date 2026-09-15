import os,System,scriptcontext as sc,Rhino
from Grasshopper import Instances
from Grasshopper.Kernel import GH_Document
from System.Reflection import BindingFlags
from System.Drawing import PointF
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static
if 'PolymitaOriginalDoc' not in sc.sticky:
 sc.sticky['PolymitaOriginalDoc']=Instances.ActiveCanvas.Document
 sc.sticky['PolymitaOriginalCanvasView']=Instances.ActiveCanvas.Viewport.Duplicate()
 sc.sticky['PolymitaOriginalSelection']=[o.Id for o in Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(False,False)]
for a in System.AppDomain.CurrentDomain.GetAssemblies():
 r=a.GetType('Polymita.ShelfRuntime')
 if r is not None:
  editor=r.GetField('editor',flags).GetValue(None)
  if editor is not None and not editor.IsDisposed:raise Exception('Close the library editor first.')
  r.GetMethod('Shutdown',flags).Invoke(None,None)
a=System.Reflection.Assembly.LoadFile(os.path.join(root,'test-output','Polymita.PolymitaTest01.dll'))
a.GetType('VisualRegressionTests').GetMethod('Run').Invoke(None,System.Array[System.Object]([root]))
if Instances.ActiveCanvas.Document is None:
 doc=GH_Document()
 proxy=next(p for p in Instances.ComponentServer.ObjectProxies if p.Desc.Name=='Sphere')
 component=Instances.ComponentServer.EmitObjectProxy(proxy.Guid).CreateInstance()
 component.CreateAttributes();component.Attributes.Pivot=PointF(250,200);doc.AddObject(component,False)
 Instances.DocumentServer.AddDocument(doc);Instances.ActiveCanvas.Document=doc;doc.NewSolution(False)
 sc.sticky['PolymitaFixtureDoc']=doc
 Instances.ActiveCanvas.Viewport.Zoom=1.5
sc.sticky['PolymitaTestObject']=Rhino.RhinoDoc.ActiveDoc.Objects.AddSphere(Rhino.Geometry.Sphere(Rhino.Geometry.Point3d.Origin,10))
sc.sticky['PolymitaTestDoc']=Rhino.RhinoDoc.ActiveDoc
sc.sticky['PolymitaBeforeLine']=set(o.Id for o in Rhino.RhinoDoc.ActiveDoc.Objects)
r=a.GetType('Polymita.ShelfRuntime');r.GetMethod('Initialize').Invoke(None,None)
r.GetMethod('OpenToolbox',flags).Invoke(None,System.Array[System.Object]([System.Int32(0)]))
sc.sticky['PolymitaAssembly']=a
Instances.ActiveCanvas.Refresh()
print('Polymita prototype ready for native interaction testing.')
