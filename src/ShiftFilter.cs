using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace WireShelf
{
    // Rhino has a native message pump, so WinForms IMessageFilter does not see its
    // keyboard messages. This hook belongs only to Rhino's current UI thread.
    internal sealed class ShiftFilter : IDisposable
    {
        private delegate IntPtr KeyboardProc(int code, IntPtr key, IntPtr state);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int type, KeyboardProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr key, IntPtr state);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetFocus();
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        private readonly KeyboardProc callback;
        private readonly ShiftTap tap = new ShiftTap();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private IntPtr hook;
        private GH_Canvas context;
        private GH_Document document;
        internal ShiftFilter()
        {
            callback = OnKey;
            hook = SetWindowsHookEx(2, callback, IntPtr.Zero, GetCurrentThreadId());
            if (hook == IntPtr.Zero) throw new InvalidOperationException("The Double Shift shortcut could not be enabled.");
            Application.ApplicationExit += OnExit;
        }
        internal void Cancel() { tap.Reset(); }
        private bool InCanvas(GH_Canvas canvas, bool allowAlt = false)
        {
            var host = Instances.DocumentEditor;
            if (canvas == null || canvas.IsDisposed || canvas.Document == null || host == null || GetForegroundWindow() != host.Handle ||
                !canvas.ClientRectangle.Contains(canvas.PointToClient(Cursor.Position)) || canvas.ActiveInteraction != null || Control.MouseButtons != MouseButtons.None ||
                (Control.ModifierKeys & (allowAlt ? Keys.Control | Keys.Shift : Keys.Control | Keys.Alt)) != Keys.None) return false;
            // Inline editors must keep their keyboard. Grasshopper itself often
            // leaves focus in the toolbar's zoom field while the canvas is active.
            var focused = Control.FromChildHandle(GetFocus());
            if (focused is TextBoxBase && !InsideToolbar(focused)) return false;
            return true;
        }
        private static bool InsideToolbar(Control control)
        { for (var c = control; c != null; c = c.Parent) if (c is ToolStrip) return true; return false; }
        private IntPtr OnKey(int code, IntPtr keyValue, IntPtr state)
        {
            if (code == 0) {
                try {
                    var canvas = Instances.ActiveCanvas;
                    var operationKey=(Keys)keyValue.ToInt32(); var operationFlags=state.ToInt64();
                    var operation=Operation(operationKey);
                    if(operation!=null && (Control.ModifierKeys & Keys.Alt)!=0 && InCanvas(canvas,true)) {
                        tap.Reset();
                        if((operationFlags & ((1L<<31)|(1L<<30)))==0) {
                            var current=canvas.Document;
                            canvas.BeginInvoke(new Action(delegate { if(!canvas.IsDisposed && canvas.Document==current) ShelfRuntime.RunOperation(operation); }));
                        }
                        return new IntPtr(1);
                    }
                    if (!InCanvas(canvas)) tap.Reset();
                    else {
                        if (canvas != context || canvas.Document != document) { tap.Reset(); context = canvas; document = canvas.Document; }
                        var key = (Keys)keyValue.ToInt32(); var flags = state.ToInt64();
                        if (key != Keys.ShiftKey && key != Keys.LShiftKey && key != Keys.RShiftKey) tap.Reset();
                        else if ((flags & (1L << 31)) == 0) {
                            if ((flags & (1L << 30)) == 0) tap.Press(clock.ElapsedMilliseconds);
                        }
                        else if (tap.Release(clock.ElapsedMilliseconds, SystemInformation.DoubleClickTime)) {
                            var currentDocument = canvas.Document;
                            canvas.BeginInvoke(new Action(delegate {
                                if (InCanvas(canvas) && canvas.Document == currentDocument) Ui.Safe(() => ShelfRuntime.OpenAtCursor(canvas));
                            }));
                        }
                    }
                } catch { tap.Reset(); } // Never propagate an exception through a native hook.
            }
            return CallNextHookEx(hook, code, keyValue, state);
        }
        // Swallowed here so Grasshopper never sees the Alt accelerator.
        private static string Operation(Keys key)
        {
            switch(key) {
                case Keys.W: return "connect";
                case Keys.Q: return "duplicate";
                case Keys.A: return "before";
                case Keys.D: return "after";
                case Keys.G: return "absorb";
                default: return null;
            }
        }
        private void OnExit(object sender, EventArgs e) { Dispose(); }
        public void Dispose()
        {
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            Application.ApplicationExit -= OnExit; tap.Reset();
        }
    }
}

