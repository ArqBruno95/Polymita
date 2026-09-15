import os,System,scriptcontext as sc,Rhino
from Grasshopper import Instances
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance
for old in System.AppDomain.CurrentDomain.GetAssemblies():
 r=old.GetType('Polymita.ShelfRuntime')
 if r is None:continue
 tb=r.GetField('toolbox',flags).GetValue(None)
 if tb is not None:
  pane=tb.GetType().GetField('viewport',flags).GetValue(tb)
  if pane is not None:pane.Width=900
 r.GetMethod('Shutdown',flags).Invoke(None,None)
doc=sc.sticky.get('PolymitaTestDoc')
if doc is not None:
 for key in ['PolymitaTestObject','PolymitaTestLine']:
  identity=sc.sticky.get(key)
  if identity is not None and doc.Objects.FindId(identity) is not None:doc.Objects.Delete(identity,True)
 for identity in sc.sticky.get('PolymitaOriginalSelection',[]):
  obj=doc.Objects.FindId(identity)
  if obj is not None:obj.Select(True)
 doc.Views.Redraw()
fixture=sc.sticky.get('PolymitaFixtureDoc')
if fixture is not None:
 if Instances.ActiveCanvas.Document==fixture:
  Instances.ActiveCanvas.Document=sc.sticky.get('PolymitaOriginalDoc')
  Instances.ActiveCanvas.Viewport.Set(sc.sticky['PolymitaOriginalCanvasView'])
 Instances.DocumentServer.RemoveDocument(fixture);fixture.Dispose();del sc.sticky['PolymitaFixtureDoc']
path=os.path.join(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),'Grasshopper','Libraries','Polymita','Polymita.gha')
a=System.Reflection.Assembly.LoadFrom(path)
a.GetType('Polymita.ShelfRuntime').GetMethod('Initialize').Invoke(None,None)
Instances.ActiveCanvas.Refresh()
def finish(sender,e):
 if Rhino.Commands.Command.InCommand():return
 Rhino.RhinoApp.Idle-=finish
 remaining=[v for v in Rhino.RhinoDoc.ActiveDoc.Views if v.MainViewport.Name=='Polymita']
 with open(os.path.join(root,'test-output','polymita-delivery.txt'),'w') as f:
  f.write(a.FullName+'\n'+a.Location+'\nRemaining test Rhino objects: '+str(sum(1 for key in ['PolymitaTestObject','PolymitaTestLine'] if doc.Objects.FindId(sc.sticky[key]) is not None))+'\nRemaining Polymita views after closing: '+str(len(remaining)))
sc.sticky['PolymitaFinish']=finish
Rhino.RhinoApp.Idle+=finish
print('Polymita 0.6.0 installed and active. Test geometry removed; original canvas restored.')
