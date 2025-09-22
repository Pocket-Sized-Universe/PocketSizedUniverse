using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class MoodlesData : IDataFile
{
    public static MoodlesData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<MoodlesData>(data);
    }
    public static string Filename { get; } = "Moodles.dat";
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
    public int Version { get; set; } = 1;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;
    public string MoodlesState { get; init; } = string.Empty;

    public bool ApplyData(RemotePlayerData ctx, bool force = false)
    {
        // Always cache
        var existing = ctx.MoodlesData;
        ctx.MoodlesData = this;

        if (ctx.Player == null)
            return false; // do not log

        var current = PsuPlugin.MoodlesService.GetStatusManager(ctx.Player.Address);
        var changed = existing == null
                      || !string.Equals(existing.MoodlesState, MoodlesState, StringComparison.Ordinal)
                      || !string.Equals(current, MoodlesState, StringComparison.Ordinal);
        if (!changed && !force)
            return false;

        if (!string.IsNullOrEmpty(MoodlesState) && !string.Equals(current, MoodlesState, StringComparison.Ordinal))
        {
            PsuPlugin.MoodlesService.SetStatusManager(ctx.Player.Address, MoodlesState);
            return true;
        }

        return false;
    }
}
