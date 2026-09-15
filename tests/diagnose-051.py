import os, System, scriptcontext as sc
from Grasshopper import Instances
from Grasshopper.Kernel import GH_FontServer
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance
canvas=Instances.ActiveCanvas
sc.sticky['Z051View']=canvas.Viewport.Duplicate()
lines=[]
for a in System.AppDomain.CurrentDomain.GetAssemblies():
 r=a.GetType('Polymita.ShelfRuntime')
 if r is None: continue
 lines.append(a.FullName)
 field=r.GetField('toolbox',flags)
 if field is None: continue
 tb=field.GetValue(None)
 if tb is None: continue
 pane=tb.GetType().GetField('viewport',flags).GetValue(tb)
 if pane is None: continue
 ctrl=pane.GetType().GetField('ViewControl',flags).GetValue(pane)
 sc.sticky['Z051Pane']=pane
 vp=ctrl.Viewport
 lines.append('CAMERA '+str(vp.CameraLocation)+' TARGET '+str(vp.CameraTarget)+' DIR '+str(vp.CameraDirection)+' bbox '+str(canvas.Document.PreviewBoundingBox))
 for p in ctrl.GetType().GetProperties():
  if p.DeclaringType.FullName.startswith('Rhino'):
   try: lines.append(str(p)+' = '+str(p.GetValue(ctrl,None)))
   except: pass
 for m in ctrl.GetType().GetMethods(flags):
  if m.DeclaringType.FullName.startswith('Rhino'): lines.append(str(m))
def paint(sender):
 g=canvas.Graphics
 with open(os.path.join(root,'test-output','paint-diagnostic.txt'),'a') as f:
  f.write('zoom='+str(canvas.Viewport.Zoom)+' transform='+str(list(g.Transform.Elements))+' dpi='+str(g.DpiY)+' page='+str(g.PageUnit)+' scale='+str(g.PageScale)+' Standard='+str(GH_FontServer.Standard)+' adjusted='+str(GH_FontServer.StandardAdjusted)+'\n')
canvas.CanvasPostPaintObjects += paint
sc.sticky['Z051Paint']=paint
with open(os.path.join(root,'test-output','viewport-diagnostic.txt'),'w') as f: f.write('\n'.join(lines))
canvas.Refresh()
