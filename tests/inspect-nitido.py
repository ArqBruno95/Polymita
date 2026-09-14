import os, System, scriptcontext as sc
from Grasshopper import Instances
from System.Reflection import BindingFlags
from System.Drawing import PointF
from Grasshopper.Kernel.Special import GH_Group
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static
assembly=next(a for a in System.AppDomain.CurrentDomain.GetAssemblies() if a.GetName().Name=='WireShelf.NitidoTest02')
runtime=assembly.GetType('WireShelf.ShelfRuntime')
box=runtime.GetField('toolbox',flags).GetValue(None)
with open(os.path.join(root,'test-output','nitido-ui.txt'),'w') as f:
 def walk(c,level):
  f.write(' '*level+c.GetType().Name+' '+str(c.Bounds)+' '+c.Text+'\n')
  if hasattr(c,'SelectedIndex'): f.write(' '*level+'SelectedIndex '+str(c.SelectedIndex)+'\n')
  for ch in c.Controls: walk(ch,level+1)
 walk(Instances.ActiveCanvas.Parent,0)
settings=runtime.GetField('toolboxSettings',flags).GetValue(None)
settings.ComponentNames=True
settings.GroupNames=True
canvas=Instances.ActiveCanvas
if canvas.Document==sc.sticky.get('WireShelfToolboxFixture'):
 doc=canvas.Document
 if not any(isinstance(o,GH_Group) for o in doc.Objects):
  group=GH_Group(); group.CreateAttributes(); group.NickName='Datos y puntos'
  for o in doc.Objects: group.AddObject(o.InstanceGuid)
  doc.AddObject(group,False)
 doc.DeselectAll(); doc.Objects[0].Attributes.Selected=True; doc.Objects[1].Attributes.Selected=True
 canvas.Invalidate()
