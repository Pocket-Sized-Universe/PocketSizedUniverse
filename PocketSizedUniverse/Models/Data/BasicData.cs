using ECommons.DalamudServices;
using Newtonsoft.Json;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class BasicData : IDataFile
{
    public static BasicData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<BasicData>(data);
    }
    public int Version { get; set; } = 1;
    public static string Filename { get; } = "Basic.dat";
    
    [JsonRequired]
    public string PlayerName { get; set; } = string.Empty;
    public uint WorldId { get; set; }
    public bool Equals(IWriteableData? x, IWriteableData? y)
    {
        return ReferenceEquals(x, y);
    }

    public int GetHashCode(IWriteableData obj)
    {
        return obj.Id.GetHashCode();
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);
}