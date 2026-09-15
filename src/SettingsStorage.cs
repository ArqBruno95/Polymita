using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Polymita
{
    // The old brand strings exist only as read-only migration inputs. All new
    // writes, exports and runtime extraction use Polymita paths.
    public sealed class SettingsStorage
    {
        private static readonly string[] PreviousFolders = { "WireShelf", "Zunzun", "Zunzún", "Nitido", "Nítido" };
        private static readonly string[] PreviousLibraries = {
            "library.wireshelf.json", "library.zunzun.json", "library.zunzún.json", "library.nitido.json", "library.nítido.json"
        };
        private readonly string settingsRoot;
        private bool prepared;
        public string DirectoryPath { get; private set; }
        public string LibraryPath { get { return Path.Combine(DirectoryPath, "library.polymita.json"); } }
        public string ToolboxPath { get { return Path.Combine(DirectoryPath, "toolbox.json"); } }

        public SettingsStorage(string settingsRoot)
        {
            this.settingsRoot=Path.GetFullPath(settingsRoot);
            DirectoryPath=Path.Combine(this.settingsRoot, "Polymita");
        }
        public void Prepare()
        {
            if (prepared) return;
            var folders=new[]{DirectoryPath}.Concat(PreviousFolders.Select(n=>Path.Combine(settingsRoot,n))).ToArray();
            var names=new[]{"library.polymita.json"}.Concat(PreviousLibraries).ToArray();
            string librarySource=Newest(folders.SelectMany(f=>names.Select(n=>Path.Combine(f,n))), LibraryPath);
            CopyFamily(librarySource, LibraryPath);
            // Prefer the settings accompanying the selected library when possible.
            string toolboxSource=librarySource==null ? null : Path.Combine(Path.GetDirectoryName(librarySource), "toolbox.json");
            if (toolboxSource==null || !File.Exists(toolboxSource))
                toolboxSource=Newest(folders.Select(f=>Path.Combine(f,"toolbox.json")), ToolboxPath);
            CopyFamily(toolboxSource, ToolboxPath);
            prepared=true;
        }
        private static string Newest(IEnumerable<string> candidates, string destination)
        {
            return candidates.Where(p=>!String.Equals(p,destination,StringComparison.OrdinalIgnoreCase) && File.Exists(p))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }
        private static void CopyFamily(string source, string destination)
        {
            // Never overwrite newer Polymita data; keep the old files as a backup.
            if (source==null || File.Exists(destination) || String.Equals(source,destination,StringComparison.OrdinalIgnoreCase)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            foreach (var backup in Directory.GetFiles(Path.GetDirectoryName(source),Path.GetFileName(source)+".*"))
            {
                var suffix=backup.Substring(source.Length);
                if (suffix!=".bak" && !suffix.StartsWith(".damaged-",StringComparison.Ordinal) &&
                    !suffix.StartsWith(".unreadable-",StringComparison.Ordinal)) continue;
                var target=destination+suffix;
                if (!File.Exists(target)) File.Copy(backup,target,false);
            }
            // The primary is published last so an interrupted migration can retry.
            File.Copy(source,destination,false);
        }
    }
}
