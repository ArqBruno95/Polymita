import os,System,scriptcontext as sc
from Grasshopper import Instances
from Grasshopper.Kernel import GH_Document
from Grasshopper.Kernel.Parameters import Param_Number
from System.Drawing import PointF
root=os.path.dirname(os.path.dirname(__file__))
flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static
if 'GestureOriginalDoc' not in sc.sticky:
 sc.sticky['GestureOriginalDoc']=Instances.ActiveCanvas.Document
 sc.sticky['GestureOriginalView']=Instances.ActiveCanvas.Viewport.Duplicate()
for a in System.AppDomain.CurrentDomain.GetAssemblies():
 r=a.GetType('WireShelf.ShelfRuntime')
 if r is not None and r.GetField('initialized',flags).GetValue(None):r.GetMethod('Shutdown').Invoke(None,None)
a=System.Reflection.Assembly.LoadFile(os.path.join(root,'test-output','Polymita.GestureTest04.dll'))
a.GetType('WireShelf.ShelfRuntime').GetMethod('Initialize').Invoke(None,None)
doc=GH_Document()
for x,y in [(180,140),(500,260)]:
 p=Param_Number();p.CreateAttributes();p.Attributes.Pivot=PointF(x,y);doc.AddObject(p,False);p.Attributes.ExpireLayout();p.Attributes.PerformLayout()
doc.Objects[1].AddSource(doc.Objects[0])
Instances.DocumentServer.AddDocument(doc);Instances.ActiveCanvas.Document=doc
Instances.ActiveCanvas.Viewport.Zoom=1.5
Instances.ActiveCanvas.Viewport.MidPoint=PointF(370,200)
sc.sticky['GestureFixture']=doc
Instances.DocumentEditor.Show();Instances.DocumentEditor.Activate();Instances.ActiveCanvas.Refresh()
