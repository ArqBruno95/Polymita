# Return to the original definition and load the release build for the current session.
import os, System, scriptcontext as sc
from Grasshopper import Instances
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static
for assembly in System.AppDomain.CurrentDomain.GetAssemblies():
 if assembly.GetName().Name.startswith('Polymita') or assembly.GetName().Name=='Polymita':
  runtime=assembly.GetType('Polymita.ShelfRuntime')
  if runtime is not None: runtime.GetMethod('Shutdown',flags).Invoke(None,None)
if 'PolymitaOriginalDoc' in sc.sticky:
 Instances.ActiveCanvas.Document=sc.sticky['PolymitaOriginalDoc']
 Instances.ActiveCanvas.Viewport.Set(sc.sticky['PolymitaOriginalViewport'])
 Instances.ActiveCanvas.Invalidate()
fixture=sc.sticky.get('PolymitaToolboxFixture')
if fixture is not None and fixture != sc.sticky.get('PolymitaOriginalDoc'):
 Instances.DocumentServer.RemoveDocument(fixture)
 fixture.Dispose()
 del sc.sticky['PolymitaToolboxFixture']
assembly=System.Reflection.Assembly.LoadFrom(os.path.join(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),'Grasshopper','Libraries','Polymita','Polymita.gha'))
assembly.GetType('Polymita.ShelfRuntime').GetMethod('Initialize').Invoke(None,None)
print('Polymita 0.4.0 ready. Original canvas restored; tools remain closed until called.')
