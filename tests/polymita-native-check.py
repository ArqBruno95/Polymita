import os,System,scriptcontext as sc,Rhino
from Grasshopper import Instances
from System.Reflection import BindingFlags
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance
log=[]
def check(ok,msg):
 if not ok:raise Exception(msg)
 log.append('PASS '+msg)
a=sc.sticky['PolymitaAssembly'];r=a.GetType('Polymita.ShelfRuntime');tb=r.GetField('toolbox',flags).GetValue(None)
pane=tb.GetType().GetField('viewport',flags).GetValue(tb)
host=pane.GetType().GetField('ViewControl',flags).GetValue(pane)
v=host.GetType().GetProperty('NativeView',flags).GetValue(host,None)
doc=sc.sticky['PolymitaTestDoc']
obj=doc.Objects.FindId(sc.sticky['PolymitaTestObject'])
check(obj is not None and obj.IsSelected(False)!=0,'Mouse selection reaches native Rhino object')
check(obj.Geometry.GetBoundingBox(True).Center.X>1,'Gumball drag changes native geometry')
added=[o for o in doc.Objects if o.Id not in sc.sticky['PolymitaBeforeLine']]
check(len(added)==1 and isinstance(added[0].Geometry,Rhino.Geometry.LineCurve),'Line command creates a native Rhino line')
sc.sticky['PolymitaTestLine']=added[0].Id
check(v.Floating,'Viewport uses a complete native floating Rhino frame')
check(v.ActiveViewportID==doc.Views.ActiveView.ActiveViewportID,'Embedded view is Rhino active view')
width=pane.Width
try:
 for size in [300,420,900]:
  pane.Width=size;pane.Parent.PerformLayout();pane.PerformLayout()
  for name in ['views','modes','command']:
   c=pane.GetType().GetField(name,flags).GetValue(pane)
   check(c.Width>50 and c.Height>=c.PreferredSize.Height and c.Parent.ClientRectangle.Contains(c.Bounds),'Unclipped '+name+' at width '+str(size))
finally:pane.Width=width;pane.Parent.PerformLayout()
views=pane.GetType().GetField('views',flags).GetValue(pane);old=views.SelectedItem
try:
 for name in ['Top','Front','Right','Perspective','Two-point']:
  views.SelectedItem=name
  check(v.ActiveViewport.CameraLocation.IsValid and v.ActiveViewport.CameraDirection.IsValid,'Native camera preset '+name)
finally:views.SelectedItem=old
modes=pane.GetType().GetField('modes',flags).GetValue(pane);oldmode=modes.SelectedItem
try:
 for name in ['Wireframe','Shaded']:
  chosen=next(m for m in modes.Items if m.EnglishName==name)
  modes.SelectedItem=chosen
  check(v.ActiveViewport.DisplayMode.Id==chosen.Id,'Display style '+name)
finally:modes.SelectedItem=oldmode
with open(os.path.join(root,'test-output','polymita-native.txt'),'w') as f:f.write('\n'.join(log)+'\n'+str(len(log))+' native interaction/layout checks passed.')
print(str(len(log))+' Polymita native interaction/layout checks passed.')
