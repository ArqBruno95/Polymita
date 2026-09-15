import os
import clr
import scriptcontext
import System
from Grasshopper import Instances
from Grasshopper.GUI import GH_CanvasMouseEvent
root = os.path.dirname(os.path.dirname(__file__))
path = os.path.join(root, "test-output", "wire-events.txt")
canvas = Instances.ActiveCanvas
def trace(sender, e):
    ev = GH_CanvasMouseEvent(sender.Viewport, e)
    a = sender.Document.FindAttributeByGrip(ev.CanvasLocation, False, True, True, 10)
    interaction = sender.ActiveInteraction
    with open(path, 'a') as f:
        f.write("event=" + str(e.Button) + " interaction=" + (str(interaction.GetType().FullName) if interaction else "null") + "\n")
        f.write("attr=" + (str(a.DocObject.Name) if a else "null") + " point=" + str(ev.CanvasLocation) + "\n")
canvas.MouseDown += trace
scriptcontext.sticky['PolymitaTrace'] = trace
with open(path, 'w') as f:
    f.write("Trace installed\n")
    for a in System.AppDomain.CurrentDomain.GetAssemblies():
        if any(s in a.GetName().Name.lower() for s in ['quick', 'wire', 'gecko']):
            f.write(str(a.FullName) + '\n')
