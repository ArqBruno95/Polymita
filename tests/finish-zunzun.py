# Return to the original definition and load the release build for the current session.
import os, System, scriptcontext as sc
from Grasshopper import Instances
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static
for assembly in System.AppDomain.CurrentDomain.GetAssemblies():
 if assembly.GetName().Name.startswith('WireShelf') or assembly.GetName().Name in ['Nitido','Zunzun']:
  runtime=assembly.GetType('WireShelf.ShelfRuntime')
  if runtime is not None: runtime.GetMethod('Shutdown',flags).Invoke(None,None)
if 'ZunzunOriginalDoc' in sc.sticky:
 Instances.ActiveCanvas.Document=sc.sticky['ZunzunOriginalDoc']
 Instances.ActiveCanvas.Viewport.Set(sc.sticky['ZunzunOriginalViewport'])
 Instances.ActiveCanvas.Invalidate()
fixture=sc.sticky.get('ZunzunFixture')
if fixture is not None and fixture != sc.sticky.get('ZunzunOriginalDoc'):
 Instances.DocumentServer.RemoveDocument(fixture)
 fixture.Dispose()
 del sc.sticky['ZunzunFixture']
assembly=System.Reflection.Assembly.LoadFrom(os.path.join(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),'Grasshopper','Libraries','Zunzun','Zunzun.gha'))
settings_type=assembly.GetType('WireShelf.ToolboxSettings')
settings=settings_type.GetMethod('Load').Invoke(None,System.Array[System.Object]([os.path.join(root,'test-output','zunzun-original-settings.json')]))
settings_type.GetMethod('Save').Invoke(settings,System.Array[System.Object]([os.path.join(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),'Grasshopper','WireShelf','toolbox.json')]))
assembly.GetType('WireShelf.ShelfRuntime').GetMethod('Initialize').Invoke(None,None)
print('Zunzun 0.5.0 ready. Original canvas restored; tools remain closed until called.')
