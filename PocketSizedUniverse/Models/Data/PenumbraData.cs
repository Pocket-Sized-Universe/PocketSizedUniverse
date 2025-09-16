using Newtonsoft.Json;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models.Mods;

namespace PocketSizedUniverse.Models.Data;

public class PenumbraData : IDataFile
{
    public static PenumbraData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<PenumbraData>(data);
    }

    public int Version { get; set; } = 2;
    public static string Filename { get; } = "Penumbra.dat";

    // Aggregated, per-character Penumbra state (matching Mare's model)
    public List<CustomRedirect> Files { get; set; } = new();
    public List<AssetSwap> FileSwaps { get; set; } = new();
    public string MetaManipulations { get; set; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

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

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);
}