import os, System, scriptcontext as sc
from Grasshopper import Instances
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static
if 'Z060OriginalZoom' not in sc.sticky:sc.sticky['Z060OriginalZoom']=Instances.ActiveCanvas.Viewport.Duplicate()
for a in System.AppDomain.CurrentDomain.GetAssemblies():
 r=a.GetType('WireShelf.ShelfRuntime')
 if r is not None:
  editor=r.GetField('editor',flags).GetValue(None)
  if editor is not None and not editor.IsDisposed:raise Exception('Close the library editor first.')
  r.GetMethod('Shutdown',flags).Invoke(None,None)
a=System.Reflection.Assembly.LoadFile(os.path.join(root,'dist-zunzun-060','Zunzun.gha'))
r=a.GetType('WireShelf.ShelfRuntime');r.GetMethod('Initialize').Invoke(None,None)
r.GetMethod('OpenToolbox',flags).Invoke(None,System.Array[System.Object]([System.Int32(0)]))
sc.sticky['Z060Assembly']=a
Instances.ActiveCanvas.Refresh()
print('Zunzun 0.6.0 native viewport test active.')
