# Run from Rhino's RunPythonScript. Uses transient GH documents and restores the
# user's active definition in finally. Does not install/initialize the plugin.
import os, clr, System, traceback
from Grasshopper import Instances
from Grasshopper.Kernel import IGH_Component, IGH_Param
root = os.path.dirname(os.path.dirname(__file__))
out = os.path.join(root, 'test-output')
assembly = System.Reflection.Assembly.LoadFrom(os.path.join(out, 'Polymita.Regression085.dll'))
canvas = Instances.ActiveCanvas
original = canvas.Document

def fingerprint(doc):
    if doc is None: return None
    rows = []
    for obj in doc.Objects:
        att = obj.Attributes
        ports = list(obj.Params.Input) if isinstance(obj, IGH_Component) else ([obj] if isinstance(obj, IGH_Param) else [])
        rows.append((str(obj.InstanceGuid), str(att.Pivot), att.Selected,
                     tuple(tuple(str(s.InstanceGuid) for s in p.Sources) for p in ports)))
    return tuple(rows)

before = fingerprint(original)
results = []
try:
    for name in ['Regression085Tests', 'GestureTests', 'OperationsTests', 'IntegrationTests', 'VisualRegressionTests']:
        try:
            assembly.GetType(name).GetMethod('Run').Invoke(None, System.Array[System.Object]([root]))
            results.append('PASS ' + name)
        except Exception as ex:
            results.append('FAIL ' + name + ': ' + str(ex))
            if hasattr(ex, 'InnerException') and ex.InnerException: results.append(str(ex.InnerException))
finally:
    assembly.GetType('Polymita.WireStyles').GetMethod('Reset').Invoke(None, None)
    canvas.Document = original
    canvas.Invalidate()
    results.append(('PASS' if before == fingerprint(original) else 'FAIL') + ' Original definition identity, object IDs, pivots, selection and input sources preserved')
    with open(os.path.join(out, 'native-summary.txt'), 'w') as f:
        f.write('\n'.join(results))
    print('\n'.join(results))
