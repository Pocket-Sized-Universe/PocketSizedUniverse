using Newtonsoft.Json.Linq;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class GlamourerData : IDataFile
{
    public int Version { get; set; } = 1;
    public static string Filename { get; } = "Glamourer.dat";
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

    public JObject GlamState { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);
}