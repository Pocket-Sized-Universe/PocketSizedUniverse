using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class CustomizeData : IDataFile
{
    public static CustomizeData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<CustomizeData>(data);
    }
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
    public static string Filename { get; } = "Customize.dat";
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);
    public string CustomizeState { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;
}