import os, System, scriptcontext as sc
from Grasshopper import Instances
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static
canvas=Instances.ActiveCanvas
if 'Z051Paint' in sc.sticky:
 canvas.CanvasPostPaintObjects-=sc.sticky['Z051Paint'];del sc.sticky['Z051Paint']
for a in System.AppDomain.CurrentDomain.GetAssemblies():
 r=a.GetType('WireShelf.ShelfRuntime')
 if r is not None:
  editor=r.GetField('editor',flags).GetValue(None)
  if editor is not None and not editor.IsDisposed: raise Exception('Close the library editor before reloading.')
  r.GetMethod('Shutdown',flags).Invoke(None,None)
a=System.Reflection.Assembly.LoadFrom(os.path.join(root,'test-output','WireShelf.Zunzun051Test01.dll'))
try:
 a.GetType('VisualRegressionTests').GetMethod('Run').Invoke(None,System.Array[System.Object]([root]))
finally:
 r=a.GetType('WireShelf.ShelfRuntime');r.GetMethod('Initialize').Invoke(None,None)
 if 'Z051View' in sc.sticky:canvas.Viewport.Set(sc.sticky['Z051View'])
 r.GetMethod('OpenToolbox',flags).Invoke(None,System.Array[System.Object]([System.Int32(0)]))
 canvas.Invalidate()
print('Zunzun 0.5.1 navigation and labels test build active.')
