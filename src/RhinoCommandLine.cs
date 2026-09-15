using Rhino;
using Rhino.Commands;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Polymita {
 // Rhino's command line, reproduced in behaviour rather than in looks. The live
 // prompt sits in front of the box the way Rhino writes it, the options Rhino is
 // offering become buttons that submit exactly what typing the option name would,
 // and the history above is Rhino's own. Anything Rhino's command line accepts —
 // a command, a coordinate, a number, an option — this one accepts too. There is
 // no Run button: Enter sends, as it does in Rhino.
 internal sealed class RhinoCommandLine : TableLayoutPanel {
  readonly Label prompt=new Label { AutoSize=true,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,
   Margin=new Padding(0,4,6,0),Text="Command" };
  readonly FlowLayoutPanel options=new FlowLayoutPanel { Dock=DockStyle.Top,AutoSize=true,
   AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Margin=new Padding(0,4,0,0),Padding=Padding.Empty };
  readonly TextBox history=new TextBox { Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,
   ScrollBars=ScrollBars.Vertical,BackColor=Color.White,Margin=new Padding(0,0,0,4),AccessibleName="Rhino command history" };
  readonly TextBox entry=new SearchBox { Hint="Enter to send…",Dock=DockStyle.Fill,
   Margin=Padding.Empty,AccessibleName="Rhino command line" };
  readonly ToolTip tips;
  readonly Func<bool> ready;
  readonly Action activate;
  string lastPrompt,lastHistory;
  string[] shown=new string[0];
  int since;

  internal RhinoCommandLine(ToolTip tips,Func<bool> ready,Action activate) {
   this.tips=tips;this.ready=ready;this.activate=activate;
   Dock=DockStyle.Top;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;
   ColumnCount=2;RowCount=3;Margin=Padding.Empty;Padding=new Padding(10,0,10,6);
   ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   RowStyles.Add(new RowStyle(SizeType.Absolute,46));
   RowStyles.Add(new RowStyle(SizeType.AutoSize));
   RowStyles.Add(new RowStyle(SizeType.AutoSize));
   prompt.ForeColor=Ui.Ink;prompt.Font=new Font("Segoe UI",8.25F,FontStyle.Bold);
   Controls.Add(history,0,0);SetColumnSpan(history,2);
   Controls.Add(prompt,0,1);Controls.Add(entry,1,1);
   Controls.Add(options,0,2);SetColumnSpan(options,2);
   tips.SetToolTip(entry,"Enter: send a Rhino command, option, number or coordinate. Escape: cancel the command.");
   tips.SetToolTip(history,"Rhino's command history.");
   tips.SetToolTip(prompt,"What Rhino is asking for.");
   entry.KeyDown+=delegate(object sender,KeyEventArgs e) {
    if(e.KeyCode==Keys.Enter) { e.SuppressKeyPress=true;Send(); }
    else if(e.KeyCode==Keys.Escape) { e.SuppressKeyPress=true;entry.Clear();Cancel(); }
   };
   SizeChanged+=delegate {
    // Assign only on a real change: an autosizing panel lays out again whenever
    // this moves, and reassigning the same value would chase its own tail. The
    // prompt is capped so a long question cannot squeeze out the box itself.
    var wrap=new Size(Math.Max(60,(ClientSize.Width-Padding.Horizontal)*2/5),0);
    if(prompt.MaximumSize!=wrap) prompt.MaximumSize=wrap;
   };
  }

  void Send() {
   if(!ready())return;
   var text=entry.Text.Trim();
   if(text.Length==0)return;
   entry.Clear();
   // A command has to start in this view or it picks in whichever Rhino view was
   // last active, which would be somewhere the user cannot see.
   activate();
   RhinoApp.SendKeystrokes(text,true);
  }
  void Cancel() { if(!ready())return;activate();RhinoApp.SendKeystrokes("\x1b",false); }
  void Choose(string option) {
   if(!ready())return;
   RhinoApp.SendKeystrokes(CommandOptions.Keystrokes(option),true);
   // Hand the cursor back so picking carries straight on after the option.
   activate();
  }

  // Driven by the pane's timer. Rhino publishes no event for the prompt changing.
  internal void Poll() {
   var text=RhinoApp.CommandPrompt;
   if(text!=lastPrompt) {
    lastPrompt=text;
    prompt.Text=string.IsNullOrEmpty(text) && Command.InCommand()?"Working…":CommandOptions.Question(text);
    Rebuild(CommandOptions.Parse(text));
    since=0;RefreshHistory();
    return;
   }
   // Output can arrive without the prompt moving, so the history is also picked
   // up on a slower beat of its own.
   if(++since<4)return;
   since=0;RefreshHistory();
  }
  void Rebuild(string[] names) {
   // Only when the offer itself changed. A prompt that rewrites a value it is
   // showing would otherwise replace the buttons under the cursor several times
   // a second, and none of them could be clicked.
   if(Same(names,shown))return;
   shown=names;
   if(names.Length==0 && options.Controls.Count==0) { options.Visible=false;return; }
   options.SuspendLayout();
   foreach(Control old in options.Controls.Cast<Control>().ToArray())
   { tips.SetToolTip(old,null);options.Controls.Remove(old);old.Dispose(); }
   foreach(var name in names) {
    var captured=name;
    var button=Ui.Button(name,delegate { Ui.Safe(delegate { Choose(captured); }); });
    button.AutoSize=true;button.Font=new Font("Segoe UI",8F);
    button.Margin=new Padding(0,0,3,3);button.MinimumSize=new Size(0,22);
    tips.SetToolTip(button,"Choose the "+CommandOptions.Keystrokes(name)+" option, as typing it would.");
    options.Controls.Add(button);
   }
   options.Visible=names.Length>0;
   options.ResumeLayout();
  }
  static bool Same(string[] a,string[] b) {
   if(a.Length!=b.Length)return false;
   for(int i=0;i<a.Length;i++) if(a[i]!=b[i])return false;
   return true;
  }
  void RefreshHistory() {
   var text=RhinoApp.CommandHistoryWindowText;
   if(text==lastHistory)return;
   lastHistory=text;
   var lines=(text==null?"":text).Replace("\r\n","\n").Split('\n');
   var take=Math.Min(lines.Length,60);
   history.Lines=lines.Skip(lines.Length-take).ToArray();
   if(!history.IsHandleCreated)return;
   history.SelectionStart=history.TextLength;
   history.ScrollToCaret();
  }
 }
}
