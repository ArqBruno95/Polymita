import os
import scriptcontext
from System.Windows.Forms import Application, IMessageFilter, Control, Form
from Grasshopper import Instances
path = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'test-output', 'shift-trace.txt')
class Trace(IMessageFilter):
    def PreFilterMessage(self, m):
        if m.Msg in [256,257,260,261]:
            c = Instances.ActiveCanvas
            with open(path, 'a') as f:
                f.write('msg={} key={} canvasFocus={} canFocus={} hostFocus={} activeForm={} activeControl={} interaction={}\n'.format(m.Msg,m.WParam,c.ContainsFocus,c.CanFocus,Instances.DocumentEditor.ContainsFocus,Form.ActiveForm,Instances.DocumentEditor.ActiveControl,c.ActiveInteraction))
        return False
if 'WireShelfShiftTrace' in scriptcontext.sticky:
    Application.RemoveMessageFilter(scriptcontext.sticky['WireShelfShiftTrace'])
scriptcontext.sticky['WireShelfShiftTrace'] = Trace()
Application.AddMessageFilter(scriptcontext.sticky['WireShelfShiftTrace'])
