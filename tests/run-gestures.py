import os,System,traceback
from Grasshopper import Instances
root=os.path.dirname(os.path.dirname(__file__))
flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static
active=[]
for a in System.AppDomain.CurrentDomain.GetAssemblies():
 r=a.GetType('Polymita.ShelfRuntime')
 if r is not None and r.GetField('initialized',flags).GetValue(None):
  active.append(r)
  r.GetMethod('Shutdown').Invoke(None,None)
try:
 a=System.Reflection.Assembly.LoadFile(os.path.join(root,'test-output','Polymita.GestureTest06.dll'))
 a.GetType('GestureTests').GetMethod('Run').Invoke(None,System.Array[System.Object]([root]))
except System.Exception as ex:
 open(os.path.join(root,'test-output','gesture-error.txt'),'w').write(traceback.format_exc()+str(ex.ToString() if hasattr(ex,"ToString") else ex))
finally:
 t=a.GetType('Polymita.WireStyles');t.GetMethod('Reset').Invoke(None,None)
 for r in active:r.GetMethod('Initialize').Invoke(None,None)
print('Gesture tests finished.')
