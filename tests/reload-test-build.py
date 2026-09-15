# Development only: detach a previous Polymita build and test an isolated assembly.
import os
import System
import scriptcontext
from Grasshopper import Instances
from System.Reflection import BindingFlags
root = os.path.dirname(os.path.dirname(__file__))
flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static
# Do not discard an unsaved library draft while replacing the development runtime.
for assembly in System.AppDomain.CurrentDomain.GetAssemblies():
    if not assembly.GetName().Name.startswith('Polymita'):
        continue
    runtime = assembly.GetType('Polymita.ShelfRuntime')
    if runtime is not None:
        editor = runtime.GetField('editor', flags).GetValue(None)
        if editor is not None and not editor.IsDisposed:
            dirty = editor.GetType().GetField('dirty', BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor)
            if dirty:
                raise Exception('Guarda o cancela primero el borrador abierto de la biblioteca Polymita.')
for assembly in System.AppDomain.CurrentDomain.GetAssemblies():
    if not assembly.GetName().Name.startswith('Polymita'):
        continue
    runtime = assembly.GetType('Polymita.ShelfRuntime')
    if runtime is None:
        continue
    shutdown = runtime.GetMethod('Shutdown', flags)
    if shutdown is not None:
        shutdown.Invoke(None, None)
        continue
    runtime.GetField('Enabled', flags).SetValue(None, False)
    for canvas in list(runtime.GetField('canvases', flags).GetValue(None)):
        runtime.GetMethod('Detach', flags).Invoke(None, System.Array[System.Object]([canvas]))
    for field in ['editor', 'palette', 'menu', 'startup']:
        value = runtime.GetField(field, flags).GetValue(None)
        if value is not None:
            value.Dispose()
if 'PolymitaTrace' in scriptcontext.sticky:
    Instances.ActiveCanvas.MouseDown -= scriptcontext.sticky['PolymitaTrace']
    del scriptcontext.sticky['PolymitaTrace']
if 'PolymitaShiftTrace' in scriptcontext.sticky:
    from System.Windows.Forms import Application
    Application.RemoveMessageFilter(scriptcontext.sticky['PolymitaShiftTrace'])
    del scriptcontext.sticky['PolymitaShiftTrace']
test = System.Reflection.Assembly.LoadFrom(os.path.join(root, 'test-output', 'Polymita.TestBuild04.dll'))
test.GetType('Polymita.ShelfRuntime').GetMethod('Initialize').Invoke(None, None)
test.GetType('Polymita.ShelfRuntime').GetProperty('Library').GetValue(None, None)
try:
    test.GetType('IntegrationTests').GetMethod('Run').Invoke(None, System.Array[System.Object]([root]))
except Exception as ex:
    print(str(ex))
else:
    print('Polymita: tests passed. New development build active.')
