using Newtonsoft.Json;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models.Mods;

namespace PocketSizedUniverse.Models.Data;

public class PenumbraWriteableData : IDataFile
{
    public int Version { get; set; } = 1;
    public static string Filename { get; } = "Penumbra.dat";
    [JsonIgnore]
    public Guid? CollectionId { get; set; }

    public List<CustomRedirect> CustomFiles { get; set; } = new();
    public List<AssetSwap> AssetSwaps { get; set; } = new();

    public bool Equals(IWriteableData? x, IWriteableData? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Id == y.Id;
    }

    public int GetHashCode(IWriteableData obj)
    {
        return obj.Id.GetHashCode();
    }
    public string MetaManipulations { get; set; } = string.Empty;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);
}