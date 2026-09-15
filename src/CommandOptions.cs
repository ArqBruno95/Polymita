using System;
using System.Collections.Generic;
namespace Polymita {
 // Reading Rhino's command line. Rhino writes the options it is offering into the
 // prompt itself, inside one pair of brackets at the end, which is the only place
 // a plug-in can see them; an option carrying a value is written Name=Value, and
 // typing the name alone is what chooses it. Kept apart from any window so the
 // parsing can be tested without Rhino or Grasshopper running.
 internal static class CommandOptions {
  static readonly char[] Separators = { ' ','\t','\r','\n','(',')',',' };
  internal static string[] Parse(string prompt) {
   if(string.IsNullOrEmpty(prompt))return new string[0];
   var open=prompt.IndexOf('(');
   var close=prompt.LastIndexOf(')');
   if(open<0 || close<=open)return new string[0];
   var inner=prompt.Substring(open+1,close-open-1);
   var found=new List<string>();
   foreach(var part in inner.Split(Separators,StringSplitOptions.RemoveEmptyEntries)) {
    var token=part.Trim();
    if(token.Length==0 || token=="/" || token=="|")continue;
    if(!found.Contains(token))found.Add(token);
   }
   return found.ToArray();
  }
  // What Rhino is asking, without the bracketed option list: those become buttons
  // of their own, and repeating them in front of the box would only cost width.
  internal static string Question(string prompt) {
   if(string.IsNullOrEmpty(prompt))return "Command";
   var open=prompt.IndexOf('(');
   var head=(open>=0?prompt.Substring(0,open):prompt).Trim();
   if(head.Length==0)return "Command";
   return head.EndsWith(":")?head:head+":";
  }
  // What to type for an option. Name=Value and Name=On are chosen by their name
  // alone: Rhino then cycles the toggle or asks for the value, exactly as it does
  // when the option is clicked in Rhino's own command line.
  internal static string Keystrokes(string option) {
   if(string.IsNullOrEmpty(option))return "";
   var split=option.IndexOf('=');
   return split>0?option.Substring(0,split):option;
  }
 }
}
