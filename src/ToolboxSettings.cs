using System;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace WireShelf
{
    [DataContract]
    public sealed class ToolboxSettings
    {
        [DataMember] public bool Polylines;
        [DataMember] public bool Highlight;
        [DataMember] public int SelectedArgb = Color.FromArgb(255, 222, 76, 36).ToArgb();
        [DataMember] public int PanelWidth = 420;
        [DataMember] public string View = "Perspective";
        [DataMember] public Guid DisplayMode;
        [DataMember] public int WireVariant;
        [DataMember] public float WireAngle = 45;
        [DataMember] public bool ComponentNames = true;
        [DataMember] public int LabelsRevision;
        [DataMember] public string[] Shortcuts;
        [DataMember] public bool GroupNames;
        [DataMember] public bool Nicknames;
        [DataMember] public float ComponentTextSize = 12;
        [DataMember] public float GroupTextSize = 26;
        [DataMember] public float GroupZoom = 0.65F;
        [DataMember] public bool CutWires = true;
        [DataMember] public bool SnapAlign = true;
        // DataContractJsonSerializer builds the instance without running field
        // initializers, so settings files written before these members existed
        // would silently deserialize both gestures as disabled.
        [OnDeserializing] private void Defaults(StreamingContext context)
        { CutWires = true; SnapAlign = true; ComponentNames = true; }
        public static ToolboxSettings Load(string path)
        {
            if (!File.Exists(path)) return new ToolboxSettings();
            using (var stream = File.OpenRead(path))
            {
                var value = (ToolboxSettings)new DataContractJsonSerializer(typeof(ToolboxSettings)).ReadObject(stream);
                if (value == null) throw new InvalidDataException("Tool settings are empty.");
                value.PanelWidth = Math.Max(300, Math.Min(900, value.PanelWidth));
                value.WireVariant = value.WireVariant == 1 ? 1 : 0;
                if (value.WireAngle < 5 || value.WireAngle > 160) value.WireAngle = 45;
                if (value.ComponentTextSize < 8 || value.ComponentTextSize > 30) value.ComponentTextSize = 12;
                if (value.GroupTextSize < 14 || value.GroupTextSize > 72) value.GroupTextSize = 26;
                if (value.GroupZoom < 0.1F || value.GroupZoom > 1) value.GroupZoom = 0.65F;
                return value;
            }
        }
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = File.Create(temp)) new DataContractJsonSerializer(typeof(ToolboxSettings)).WriteObject(stream, this);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}

