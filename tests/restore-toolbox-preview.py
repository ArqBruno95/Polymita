import scriptcontext as sc
from Grasshopper import Instances
if 'PolymitaOriginalDoc' in sc.sticky:
    Instances.ActiveCanvas.Document = sc.sticky['PolymitaOriginalDoc']
    Instances.ActiveCanvas.Viewport.Set(sc.sticky['PolymitaOriginalViewport'])
    Instances.ActiveCanvas.Invalidate()
    print('Original definition and canvas view restored.')
