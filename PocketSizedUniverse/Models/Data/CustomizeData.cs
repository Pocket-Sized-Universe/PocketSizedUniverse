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
    public string CustomizeState { get; init; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

    public bool ApplyData(RemotePlayerData ctx)
    {
        // Always cache
        ctx.CustomizeData = this;

        if (ctx.Player == null)
            return false;

        var current = PsuPlugin.CustomizeService.GetActiveProfileOnCharacter(ctx.Player.ObjectIndex);
        if (current.Item1 > 0 || current.Item2 == null || current.Item2 == Guid.Empty)
            return false;
        var currentProfile = PsuPlugin.CustomizeService.GetCustomizeProfileByUniqueId(current.Item2.Value);
        var changed = ctx.CustomizeData == null
            || !string.Equals(ctx.CustomizeData.CustomizeState, CustomizeState, StringComparison.Ordinal)
                || !string.Equals(currentProfile.Item2, CustomizeState, StringComparison.Ordinal);
        if (ctx.AssignedCustomizeProfileId == null && !string.IsNullOrEmpty(CustomizeState))
        {
            var apply = PsuPlugin.CustomizeService
                .ApplyTemporaryCustomizeProfileOnCharacter(ctx.Player.ObjectIndex, CustomizeState);
            if (apply.Item1 > 0)
            {
                // failed to apply; treat as no-op
                return false;
            }

            ctx.AssignedCustomizeProfileId = apply.Item2;
            return true;
        }

        if (ctx.AssignedCustomizeProfileId != null && string.IsNullOrEmpty(CustomizeState))
        {
            var remove = PsuPlugin.CustomizeService
                .DeleteTemporaryCustomizeProfileOnCharacter(ctx.Player.ObjectIndex);
            if (remove > 0)
                return false;
            return true;
        }

        return false;
    }
}
