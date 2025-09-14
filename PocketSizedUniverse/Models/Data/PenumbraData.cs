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
            return new PenumbraData();
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<PenumbraData>(data);
    }
    public int Version { get; set; } = 1;
    public static string Filename { get; } = "Penumbra.dat";

    public List<SyncedMod> Mods { get; set; } = new();

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