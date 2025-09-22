using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class HonorificData : IDataFile
{
    public static HonorificData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<HonorificData>(data);
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
    public static string Filename { get; } = "Honorific.dat";
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);
    public string Title { get; init; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

    public bool ApplyData(RemotePlayerData ctx, bool force = false)
    {
        // Always cache
        var existing = ctx.HonorificData;
        ctx.HonorificData = this;

        if (ctx.Player == null)
            return false;

        var current = PsuPlugin.HonorificService.GetCharacterTitle(ctx.Player.ObjectIndex);
        var changed = existing == null
                      || !string.Equals(existing.Title, Title, StringComparison.Ordinal)
                      || !string.Equals(current, Title, StringComparison.Ordinal);
        if (!changed && !force)
            return false;

        if (!string.IsNullOrEmpty(Title) && !string.Equals(current, Title, StringComparison.Ordinal))
        {
            PsuPlugin.HonorificService.SetCharacterTitle(ctx.Player.ObjectIndex, Title);
            return true;
        }

        return false;
    }
}
