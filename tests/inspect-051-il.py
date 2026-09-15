import os, System, scriptcontext as sc
from System.Reflection import BindingFlags
from System.Reflection.Emit import OpCodes, OperandType
from Grasshopper import Instances
root=os.path.dirname(os.path.dirname(__file__))
flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance
ops={}
for f in System.Type.GetType('System.Reflection.Emit.OpCodes').GetFields():
 o=f.GetValue(None); ops[int(o.Value)&65535]=o
lines=[]
def dump(m):
 lines.append(str(m)); b=m.GetMethodBody()
 if b is None:return
 data=b.GetILAsByteArray(); i=0
 while i<len(data):
  start=i; n=int(data[i]); i+=1
  if n==254:n=65280+int(data[i]); i+=1
  o=ops[n]; k=str(o.OperandType); operand=''; size=0
  if k in ['InlineField','InlineMethod','InlineType','InlineTok','InlineString','InlineSig']:
   token=System.BitConverter.ToInt32(data,i);size=4
   try: operand=str(m.Module.ResolveString(token) if k=='InlineString' else m.Module.ResolveMember(token))
   except: operand=str(token)
  elif k in ['InlineI','InlineBrTarget','ShortInlineR']:size=4;operand=str(System.BitConverter.ToInt32(data,i))
  elif k in ['InlineI8','InlineR']:size=8
  elif k in ['ShortInlineI','ShortInlineVar','ShortInlineBrTarget']:size=1;operand=str(data[i])
  elif k=='InlineVar':size=2
  elif k=='InlineSwitch':size=4+4*System.BitConverter.ToInt32(data,i)
  i+=size;lines.append(str(start)+' '+str(o)+' '+operand)
ctrl=sc.sticky['Z051Pane'].ViewControl if False else sc.sticky['Z051Pane'].GetType().GetField('ViewControl',flags).GetValue(sc.sticky['Z051Pane'])
for name in ['OnMouseMove','OnMouseWheel']:dump(ctrl.GetType().GetMethod(name,flags))
canvas=Instances.ActiveCanvas
for name in ['OnPaint']:
 dump(canvas.GetType().GetMethod(name,flags))
with open(os.path.join(root,'test-output','native-il.txt'),'w') as f:f.write('\n'.join(lines))
