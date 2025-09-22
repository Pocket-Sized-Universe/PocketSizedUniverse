using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class GlamourerData : IDataFile
{
    public static GlamourerData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<GlamourerData>(data);
    }

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

    public string GlamState { get; init; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);

    public bool ApplyData(RemotePlayerData ctx, bool force = false)
    {
        if (ctx.Player == null)
            return false; // no logging to avoid spam
        var current = PsuPlugin.GlamourerService.GetStateBase64.Invoke(ctx.Player.ObjectIndex, ctx.LockKey).Item2;
        var changed = ctx.GlamourerData == null
                      || !string.Equals(ctx.GlamourerData.GlamState, GlamState, StringComparison.Ordinal)
                      || !string.Equals(current, GlamState, StringComparison.Ordinal);
        if (!changed && !force)
            return false;

        // Cache new state always
        ctx.GlamourerData = this;


        PsuPlugin.GlamourerService.ApplyState.Invoke(GlamState, ctx.Player.ObjectIndex, ctx.LockKey);
        return true;
    }
}
