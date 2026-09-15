# Run from Rhino's RunPythonScript. Uses an isolated Canvas and transient documents;
# it does not replace the open definition or register a second copy of the plug-in.
import os, System, traceback
from System.Windows.Forms import Timer, Control, Keys, MouseButtons
root = os.path.dirname(os.path.dirname(__file__))
out = os.path.join(root, 'test-output')
def run(sender, args):
    if Control.ModifierKeys != Keys.None or Control.MouseButtons != MouseButtons.None:
        return
    sender.Stop()
    try:
        assembly = System.Reflection.Assembly.Load(System.IO.File.ReadAllBytes(os.path.join(out, 'Polymita.CanvasTests.dll')))
        assembly.GetType('WireGestureTests').GetMethod('Run').Invoke(None, System.Array[System.Object]([root]))
        result = 'PASS: wire gesture suite completed.'
    except System.Exception as ex:
        result = str(ex.ToString())
    except Exception:
        result = traceback.format_exc()
    finally:
        sender.Dispose()
    with open(os.path.join(out, 'wire-gesture-summary.txt'), 'w') as f:
        f.write(result)
    print(result)
timer = Timer()
timer.Interval = 300
timer.Tick += run
timer.Start()
