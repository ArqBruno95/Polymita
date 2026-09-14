import scriptcontext as sc
from Grasshopper import Instances
if 'WireShelfOriginalDoc' in sc.sticky:
    Instances.ActiveCanvas.Document = sc.sticky['WireShelfOriginalDoc']
    Instances.ActiveCanvas.Viewport.Set(sc.sticky['WireShelfOriginalViewport'])
    Instances.ActiveCanvas.Invalidate()
    print('Original definition and canvas view restored.')
