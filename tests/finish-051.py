import os, System, scriptcontext as sc
from Grasshopper import Instances
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static
path=os.path.join(root,'dist-polymita-051','Polymita.gha')
# A separate load context allows replacing a loaded release without restarting Rhino.
a=System.Reflection.Assembly.LoadFile(path)
if str(a.GetName().Version)!='0.5.1.0':raise Exception('Unexpected release version')
for old in System.AppDomain.CurrentDomain.GetAssemblies():
 if old==a:continue
 r=old.GetType('Polymita.ShelfRuntime')
 if r is not None:r.GetMethod('Shutdown',flags).Invoke(None,None)
r=a.GetType('Polymita.ShelfRuntime');r.GetMethod('Initialize').Invoke(None,None)
canvas=Instances.ActiveCanvas
if 'Z051View' in sc.sticky:canvas.Viewport.Set(sc.sticky['Z051View'])
r.GetMethod('OpenToolbox',flags).Invoke(None,System.Array[System.Object]([System.Int32(0)]))
canvas.Refresh()
with open(os.path.join(root,'test-output','active-051.txt'),'w') as f:f.write(a.FullName+'\n'+a.Location+'\nobjects='+str(canvas.Document.ObjectCount)+'\nzoom='+str(canvas.Viewport.Zoom))
print('Polymita 0.5.1 active. Definition and library retained.')
