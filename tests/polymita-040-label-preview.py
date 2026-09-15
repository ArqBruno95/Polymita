import os, System, scriptcontext as sc
from Grasshopper import Instances
from Grasshopper.Kernel.Special import GH_Group
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
canvas=Instances.ActiveCanvas
flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static
assembly=next(a for a in System.AppDomain.CurrentDomain.GetAssemblies() if a.GetName().Name=='Polymita.PolymitaTest03')
runtime=assembly.GetType('Polymita.ShelfRuntime')
settings=runtime.GetField('toolboxSettings',flags).GetValue(None)
settings.GroupNames=True
styles=assembly.GetType('Polymita.WireStyles')
styles.GetField('Variant').SetValue(None,System.Int32(1))
styles.GetField('Angle').SetValue(None,System.Single(45))
styles.GetMethod('SetHighlight').Invoke(None,System.Array[System.Object]([True,System.Drawing.Color.OrangeRed]))
if canvas.Document==sc.sticky.get('PolymitaToolboxFixture'):
 doc=canvas.Document
 group=GH_Group(); group.CreateAttributes(); group.NickName='Datos y puntos'
 for o in doc.Objects: group.AddObject(o.InstanceGuid)
 doc.AddObject(group,False); canvas.Viewport.Zoom=0.6; canvas.Viewport.Focus(group.Attributes); canvas.Invalidate()
 print('Polymita labels and angular wire preview ready.')
