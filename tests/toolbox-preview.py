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
    if assembly.GetName().Name.startswith('Polymita'):
        runtime = assembly.GetType('Polymita.ShelfRuntime')
        if runtime is not None:
            editor = runtime.GetField('editor', flags).GetValue(None)
            if editor is not None and not editor.IsDisposed and editor.GetType().GetField('dirty', BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor):
                raise Exception('Save the library draft first.')
            runtime.GetMethod('Shutdown', flags).Invoke(None, None)

assembly = System.Reflection.Assembly.LoadFrom(os.path.join(root, 'test-output', 'Polymita.ToolboxTest04.dll'))
runtime = assembly.GetType('Polymita.ShelfRuntime')
runtime.GetMethod('Initialize').Invoke(None, None)
assembly.GetType('IntegrationTests').GetMethod('Run').Invoke(None, System.Array[System.Object]([root]))
if 'PolymitaToolboxFixture' not in sc.sticky:
    doc = GH_Document()
    panel = GH_Panel(); panel.CreateAttributes(); panel.UserText = '1'; panel.NickName = 'Datos de prueba'
    panel.Attributes.Pivot = PointF(100,100); doc.AddObject(panel, False)
    for x,y in [(380,100),(550,270),(280,400)]:
        param = Param_Number(); param.CreateAttributes(); param.Attributes.Pivot = PointF(x,y)
        param.AddSource(panel); doc.AddObject(param,False)
    point = Param_Point(); point.CreateAttributes(); point.Attributes.Pivot = PointF(100,300)
    point.PersistentData.Append(GH_Point(Point3d(5,5,3)))
    doc.AddObject(point,False); doc.NewSolution(False)
    Instances.DocumentServer.AddDocument(doc)
    sc.sticky['PolymitaToolboxFixture'] = doc
canvas.Document = sc.sticky['PolymitaToolboxFixture']
from Grasshopper.Kernel import GH_ProfilerMode
canvas.Document.Profiler = GH_ProfilerMode.Processor
canvas.Document.Objects[0].UserText = '1'
canvas.Document.NewSolution(False)
canvas.Viewport.Zoom = 1.0
try:
    assembly.GetType('ToolboxNativeTests').GetMethod('Run').Invoke(None, System.Array[System.Object]([root]))
except Exception as error:
    with open(os.path.join(root, 'test-output', 'toolbox-native-error.txt'), 'w') as report:
        report.write(str(error))
        if hasattr(error, 'InnerException'): report.write(str(error.InnerException))
    print('Native tests require review; see toolbox-native-error.txt')
runtime.GetMethod('OpenToolbox', flags).Invoke(None, System.Array[System.Object]([System.Int32(0)]))
with open(os.path.join(root, 'test-output', 'toolbox-native.txt'), 'w') as report:
    report.write('Runtime: ' + str(System.Environment.Version) + '\n')
    for control in canvas.Parent.Controls:
        report.write(control.Name + ': ' + str(control.Dock) + ' ' + str(control.Bounds) + '\n')
    report.write('Original document preserved; fixture active.\n')
print('Polymita toolbox fixture ready. Original definition retained with native backup.')
