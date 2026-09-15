import os,System,scriptcontext as sc
import Rhino
from Grasshopper import Instances
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
sc.sticky['Z060BeforeObjects']=set(o.Id for o in Rhino.RhinoDoc.ActiveDoc.Objects)
sc.sticky['Z060BeforeSelection']=[o.Id for o in Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(False,False)]
sc.sticky['Z060Document']=Rhino.RhinoDoc.ActiveDoc
flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance
a=sc.sticky['Z060Assembly'];r=a.GetType('Polymita.ShelfRuntime')
tb=r.GetField('toolbox',flags).GetValue(None)
pane=tb.GetType().GetField('viewport',flags).GetValue(tb)
host=pane.GetType().GetField('ViewControl',flags).GetValue(pane)
sc.sticky['Z060NativeHost']=host
v=host.GetType().GetProperty('NativeView',flags).GetValue(host,None)
sc.sticky['Z060Camera']=v.ActiveViewport.GetType()
lines=['native view='+str(v.ActiveViewportID),'floating='+str(v.Floating),'objects='+str(len(sc.sticky['Z060BeforeObjects']))]
brand=a.GetType('Polymita.Brand')
for key in ['Main','Labels']:brand.GetField(key,flags).GetValue(None).Save(os.path.join(root,'test-output','icon-'+key+'.png'))
with open(os.path.join(root,'test-output','native-060-start.txt'),'w') as f:f.write('\n'.join(lines))
print('Native viewport test snapshot saved.')
