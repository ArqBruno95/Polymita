# Return to the original definition and load the release build for the current session.
import os, System, scriptcontext as sc
from Grasshopper import Instances
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static
for assembly in System.AppDomain.CurrentDomain.GetAssemblies():
 if assembly.GetName().Name.startswith('WireShelf') or assembly.GetName().Name=='Nitido':
  runtime=assembly.GetType('WireShelf.ShelfRuntime')
  if runtime is not None: runtime.GetMethod('Shutdown',flags).Invoke(None,None)
if 'WireShelfOriginalDoc' in sc.sticky:
 Instances.ActiveCanvas.Document=sc.sticky['WireShelfOriginalDoc']
 Instances.ActiveCanvas.Viewport.Set(sc.sticky['WireShelfOriginalViewport'])
 Instances.ActiveCanvas.Invalidate()
fixture=sc.sticky.get('WireShelfToolboxFixture')
if fixture is not None and fixture != sc.sticky.get('WireShelfOriginalDoc'):
 Instances.DocumentServer.RemoveDocument(fixture)
 fixture.Dispose()
 del sc.sticky['WireShelfToolboxFixture']
assembly=System.Reflection.Assembly.LoadFrom(os.path.join(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),'Grasshopper','Libraries','Nitido','Nitido.gha'))
assembly.GetType('WireShelf.ShelfRuntime').GetMethod('Initialize').Invoke(None,None)
print('Nítido 0.4.0 ready. Original canvas restored; tools remain closed until called.')
