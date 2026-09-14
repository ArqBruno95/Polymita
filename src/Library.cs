// WireShelf — 2026. GPL-3.0-or-later. See COPYING and THIRD_PARTY_NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace WireShelf
{
    [DataContract]
    public sealed class ShelfLibrary
    {
        [DataMember] public int Version = 1;
        [DataMember(EmitDefaultValue = false)] public int CatalogRevision;
        [DataMember(EmitDefaultValue = false)] public int OperationsRevision;
        [DataMember(EmitDefaultValue = false)] public int EnglishRevision;
        [DataMember] public List<ShelfSection> Sections = new List<ShelfSection>();
        public IEnumerable<ShelfSection> Search(string query)
        {
            query = (query ?? "").Trim();
            foreach (var section in Sections)
            {
                var items = section.Items.Where(x => query.Length == 0 ||
                    Contains(section.Title, query) || Contains(x.Name, query) || Contains(x.Notes, query)).ToList();
                if (items.Count > 0 || query.Length == 0)
                    yield return new ShelfSection { Id = section.Id, Title = section.Title, Items = items };
            }
        }
        private static bool Contains(string value, string term)
        { return (value ?? "").IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0; }
    }

    [DataContract]
    public sealed class ShelfSection
    {
        [DataMember] public Guid Id = Guid.NewGuid();
        [DataMember] public string Title = "Favorites";
        [DataMember] public List<ShelfItem> Items = new List<ShelfItem>();
        public override string ToString() { return Title; }
    }

    [DataContract]
    public sealed class ShelfItem
    {
        [DataMember] public Guid Id = Guid.NewGuid();
        [DataMember] public Guid ComponentId;
        [DataMember(EmitDefaultValue = false)] public string ActionId;
        public bool IsAction { get { return !String.IsNullOrEmpty(ActionId); } }
        [DataMember] public string Name = "Component";
        [DataMember] public string Notes = "";
        [DataMember] public string SnapshotXml = "";
        [DataMember] public int InputIndex;
        [DataMember] public int OutputIndex;
        public bool IsRecipe { get { return !String.IsNullOrEmpty(SnapshotXml); } }
        public override string ToString() { return Name; }
    }

    public sealed class LibraryStore
    {
        public const int MaxBytes = 32 * 1024 * 1024;
        public string FilePath { get; private set; }
        public bool Recovered { get; private set; }
        public LibraryStore(string path) { FilePath = Path.GetFullPath(path); }
        private static DataContractJsonSerializer Serializer()
        { return new DataContractJsonSerializer(typeof(ShelfLibrary), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 100000 }); }
        public static byte[] Encode(ShelfLibrary library)
        {
            Validate(library);
            using (var stream = new MemoryStream())
            {
                Serializer().WriteObject(stream, library);
                if (stream.Length > MaxBytes) throw new InvalidDataException("The library exceeds 32 MB.");
                return stream.ToArray();
            }
        }
        public static ShelfLibrary Decode(byte[] bytes)
        {
            if (bytes.Length > MaxBytes) throw new InvalidDataException("The library exceeds 32 MB.");
            using (var stream = new MemoryStream(bytes))
            {
                var library = (ShelfLibrary)Serializer().ReadObject(stream);
                Validate(library);
                return library;
            }
        }
        public static ShelfLibrary Copy(ShelfLibrary library) { return Decode(Encode(library)); }
        public static void Validate(ShelfLibrary library)
        {
            if (library == null || library.Version != 1 || library.Sections == null)
                throw new InvalidDataException("Unsupported library format (version 1 required).");
            if (library.Sections.Count > 200) throw new InvalidDataException("Maximum: 200 sections.");
            var ids = new HashSet<Guid>();
            var count = 0;
            foreach (var section in library.Sections)
            {
                if (section == null || section.Id == Guid.Empty || !ids.Add(section.Id) ||
                    String.IsNullOrWhiteSpace(section.Title) || section.Title.Length > 80 || section.Items == null)
                    throw new InvalidDataException("Invalid section: check its title and identifier.");
                foreach (var item in section.Items)
                {
                    if (item == null || item.Id == Guid.Empty || !ids.Add(item.Id) ||
                        (item.IsAction ? (item.ActionId != "connect" && item.ActionId != "duplicate") || item.IsRecipe : item.ComponentId == Guid.Empty) ||
                        String.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 120 ||
                        item.InputIndex < 0 || item.OutputIndex < 0 || item.InputIndex > 10000 || item.OutputIndex > 10000 ||
                        (item.Notes ?? "").Length > 2000 || (item.SnapshotXml ?? "").Length > 8 * 1024 * 1024)
                        throw new InvalidDataException("Invalid favorite or recipe too large (maximum 8 MB).");
                    count++;
                }
            }
            if (count > 10000) throw new InvalidDataException("Maximum: 10,000 favorites.");
        }
        public ShelfLibrary Load()
        {
            Recovered = false;
            if (!File.Exists(FilePath)) return null;
            try { return Read(FilePath); }
            catch (Exception first)
            {
                if (!(first is IOException || first is SerializationException || first is System.Xml.XmlException)) throw;
                if (!File.Exists(FilePath + ".bak")) throw;
                var backup = Read(FilePath + ".bak");
                Recovered = true;
                return backup;
            }
        }
        public static ShelfLibrary Read(string path)
        {
            if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException("The library exceeds 32 MB.");
            return Decode(File.ReadAllBytes(path));
        }
        public void Save(ShelfLibrary library)
        {
            var bytes = Encode(library);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (File.Exists(FilePath))
                {
                    // Keep the last valid backup when recovering a damaged primary file.
                    if (Recovered) File.Copy(FilePath, FilePath + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true);
                    File.Replace(temp, FilePath, Recovered ? null : FilePath + ".bak");
                }
                else File.Move(temp, FilePath);
                Recovered = false;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}

