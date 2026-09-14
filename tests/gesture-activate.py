import os,System,scriptcontext as sc
from Grasshopper import Instances
root=os.path.dirname(os.path.dirname(__file__))
flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static
for a in System.AppDomain.CurrentDomain.GetAssemblies():
 r=a.GetType('WireShelf.ShelfRuntime')
 if r is not None and r.GetField('initialized',flags).GetValue(None):r.GetMethod('Shutdown').Invoke(None,None)
a=System.Reflection.Assembly.LoadFile(os.path.join(root,'test-output','Polymita.GestureTest06.dll'))
a.GetType('WireShelf.ShelfRuntime').GetMethod('Initialize').Invoke(None,None)
canvas=Instances.ActiveCanvas
def track(sender,e):
 interaction=canvas.ActiveInteraction
 if interaction is not None:
  open(os.path.join(root,'test-output','gesture-ui.txt'),'w').write(interaction.GetType().FullName+' active='+str(interaction.IsActive))
canvas.MouseMove+=track
sc.sticky['GestureTrack']=track
