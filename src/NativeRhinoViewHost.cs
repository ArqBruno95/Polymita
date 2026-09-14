using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Grasshopper;
using Rhino;
using Rhino.Display;
namespace WireShelf {
 // Keep the complete Rhino-owned floating frame intact. Reparenting its view
 // child breaks native viewport destruction/display contexts on document close.
 internal sealed class NativeRhinoViewHost : Control {
  RhinoView view; RhinoDoc document; IntPtr frame; Guid previousView;
  readonly Timer timer=new Timer { Interval=100 };
  internal event Action Ready;
  internal bool HasView { get { return view!=null && frame!=IntPtr.Zero && IsWindow(frame); } }
  internal RhinoViewport Viewport { get { return HasView?view.ActiveViewport:null; } }
  internal NativeRhinoViewHost() {
   BackColor=Color.White; AccessibleName="Interactive Rhino viewport";
   timer.Tick+=delegate { Sync(); };
   RhinoDoc.CloseDocument+=DocumentClosing;
  }
  protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e);timer.Start(); }
  void CreateView() {
   document=RhinoDoc.ActiveDoc;
   if(document==null)return;
   previousView=document.Views.ActiveView==null?Guid.Empty:document.Views.ActiveView.ActiveViewportID;
   var rect=RectangleToScreen(ClientRectangle);
   view=document.Views.Add("Polymita",DefinedViewportProjection.Perspective,rect,true);
   if(view==null)throw new InvalidOperationException("Rhino could not create the interactive view.");
   frame=GetAncestor(view.Handle,2);
   if(frame==IntPtr.Zero || frame==RhinoApp.MainWindowHandle()) { view.Close();view=null;frame=IntPtr.Zero;throw new InvalidOperationException("Rhino did not create a floating viewport frame."); }
   view.TitleVisible=false;
   long style=GetWindowLongPtr(frame,-16).ToInt64();
   SetWindowLongPtr(frame,-16,new IntPtr(style & ~0x00CF0000L));
   // Set an OWNER for this top-level frame, never a parent for the Rhino view.
   SetWindowLongPtr(frame,-8,Instances.DocumentEditor.Handle);
   SetWindowPos(frame,IntPtr.Zero,rect.X,rect.Y,rect.Width,rect.Height,0x0034);
   if(Ready!=null)Ready();
  }
  internal void Sync() {
   if(IsDisposed || !IsHandleCreated)return;
   var owner=Instances.DocumentEditor;
   bool show=Visible && owner!=null && owner.Visible && owner.WindowState!=FormWindowState.Minimized && Width>16 && Height>16;
   if(!show) { if(HasView)ShowWindow(frame,0);return; }
   if(document!=null && document!=RhinoDoc.ActiveDoc) ReleaseView();
   if(!HasView) {
    // Creating a viewport while a command is using a document is deferred.
    if(Rhino.Commands.Command.InCommand())return;
    try { CreateView(); } catch(Exception ex) { timer.Stop();Ui.Error(ex);return; }
   }
   if(!HasView)return;
   var rect=RectangleToScreen(ClientRectangle);
   NativeRect actual; GetWindowRect(frame,out actual);
   if(actual.Left!=rect.Left || actual.Top!=rect.Top || actual.Right-actual.Left!=rect.Width || actual.Bottom-actual.Top!=rect.Height)
    SetWindowPos(frame,IntPtr.Zero,rect.X,rect.Y,rect.Width,rect.Height,0x0014);
   ShowWindow(frame,4);
  }
  internal void ActivateView() {
   if(!HasView)return;
   document.Views.ActiveView=view;
   SetFocus(view.Handle);
  }
  internal void RedrawView() { if(HasView)view.Redraw(); }
  void DocumentClosing(object sender,DocumentEventArgs e) {
   if(document==null || e.Document!=document)return;
   if(HasView)ShowWindow(frame,0);
   // Rhino owns destruction during document closing.
   view=null;document=null;frame=IntPtr.Zero;
  }
  void ReleaseView() {
   var closing=view; var doc=document; var oldId=previousView;
   if(HasView)ShowWindow(frame,0);
   view=null;document=null;frame=IntPtr.Zero;
   if(closing==null || doc==null)return;
   Action close=delegate {
    if(!IsWindow(closing.Handle))return;
    var other=doc.Views.FirstOrDefault(v=>v.ActiveViewportID==oldId) ?? doc.Views.FirstOrDefault(v=>v.ActiveViewportID!=closing.ActiveViewportID);
    if(other!=null && doc.Views.ActiveView==closing)doc.Views.ActiveView=other;
    closing.Close();
   };
   if(!Rhino.Commands.Command.InCommand())close();
   else {
    EventHandler idle=null;
    idle=delegate { if(Rhino.Commands.Command.InCommand())return;RhinoApp.Idle-=idle;close(); };
    RhinoApp.Idle+=idle;
   }
  }
  protected override void Dispose(bool disposing) {
   if(disposing) { timer.Dispose();RhinoDoc.CloseDocument-=DocumentClosing;ReleaseView(); }
   base.Dispose(disposing);
  }
  [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
  [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
  [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int command);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rectangle);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
  [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
  [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr hwnd);
 }
}

