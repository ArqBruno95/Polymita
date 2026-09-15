# Development fixture: preserve the current definition in memory AND a native archive.
import os
import System
import scriptcontext as sc
from System.Reflection import BindingFlags
from System.Drawing import PointF
from Grasshopper import Instances
from Grasshopper.Kernel import GH_Document, GH_DocumentIO
from Grasshopper.Kernel.Parameters import Param_Point, Param_Number
from Grasshopper.Kernel.Special import GH_Panel
from Rhino.Geometry import Point3d
from Grasshopper.Kernel.Types import GH_Point

root = os.path.dirname(os.path.dirname(__file__))
canvas = Instances.ActiveCanvas
if 'PolymitaOriginalDoc' not in sc.sticky:
    sc.sticky['PolymitaOriginalDoc'] = canvas.Document
    sc.sticky['PolymitaOriginalViewport'] = canvas.Viewport.Duplicate()
    original = canvas.Document
    if original is not None:
        serializable = next(t for t in original.GetType().GetInterfaces() if t.Name == 'GH_ISerializable')
        archive = System.Activator.CreateInstance(serializable.Assembly.GetType('GH_IO.Serialization.GH_Archive'))
        if not archive.AppendObject(original, 'Definition'):
            raise Exception('Cannot archive the active definition.')
        backup = os.path.join(root, 'test-output', 'before-toolbox-' + System.DateTime.Now.ToString('yyyyMMdd-HHmmss') + '.gh')
        if not archive.WriteToFile(backup, False, False):
            raise Exception('Cannot write the backup; test cancelled.')
        sc.sticky['PolymitaOriginalDoc'] = original
        sc.sticky['PolymitaOriginalViewport'] = canvas.Viewport.Duplicate()

flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static
for assembly in System.AppDomain.CurrentDomain.GetAssemblies():
    if assembly.GetName().Name.startswith('Polymita') or assembly.GetName().Name in ['Polymita','Polymita']:
        runtime = assembly.GetType('Polymita.ShelfRuntime')
        if runtime is not None:
            editor = runtime.GetField('editor', flags).GetValue(None)
            if editor is not None and not editor.IsDisposed and editor.GetType().GetField('dirty', BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor):
                raise Exception('Save the library draft first.')
            runtime.GetMethod('Shutdown', flags).Invoke(None, None)

assembly = System.Reflection.Assembly.LoadFrom(os.path.join(root, 'test-output', 'Polymita.PolymitaTest01.dll'))
runtime = assembly.GetType('Polymita.ShelfRuntime')
runtime.GetMethod('Initialize').Invoke(None, None)
assembly.GetType('IntegrationTests').GetMethod('Run').Invoke(None, System.Array[System.Object]([root]))
for test in ['ToolboxNativeTests','OperationsTests']:
 assembly.GetType(test).GetMethod('Run').Invoke(None,System.Array[System.Object]([root]))
library=runtime.GetProperty('Library').GetValue(None,None)
doc=GH_Document()
a=Param_Point(); a.CreateAttributes(); a.Attributes.Pivot=PointF(150,300)
a.PersistentData.Append(GH_Point(Point3d(0,0,0))); doc.AddObject(a,False)
b=Param_Point(); b.CreateAttributes(); b.Attributes.Pivot=PointF(750,100); b.AddSource(a); doc.AddObject(b,False)
Instances.DocumentServer.AddDocument(doc); canvas.Document=doc; doc.NewSolution(False)
sc.sticky['PolymitaFixture']=doc
settings=runtime.GetField('toolboxSettings',flags).GetValue(None)
settings.ComponentNames=True; settings.WireVariant=1; settings.Polylines=True
settings.Highlight=True; settings.SelectedArgb=System.Drawing.Color.OrangeRed.ToArgb()
styles=assembly.GetType('Polymita.WireStyles')
styles.GetField('Variant').SetValue(None,System.Int32(1))
styles.GetMethod('SetPolylines').Invoke(None,System.Array[System.Object]([True]))
styles.GetMethod('SetHighlight').Invoke(None,System.Array[System.Object]([True,System.Drawing.Color.OrangeRed]))
a.Attributes.Selected=True
canvas.Viewport.Zoom=1.0
canvas.Invalidate()
print('Polymita preview ready. Original definition retained.')
