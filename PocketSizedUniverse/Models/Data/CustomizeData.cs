using Dalamud.Game.ClientState.Objects.SubKinds;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class CustomizeData : IDataFile, IEquatable<CustomizeData>
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

    public bool Equals(CustomizeData? obj)
    {
        if (obj == null) return false;
        return CustomizeState == obj.CustomizeState;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public static string Filename { get; } = "Customize.dat";
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);
    public string CustomizeState { get; init; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

    public (bool Applied, string Result) ApplyData(IPlayerCharacter player, params object[] args)
    {
        try
        {
            var current = PsuPlugin.CustomizeService.GetActiveProfileOnCharacter(player.ObjectIndex);
            if (current.Item1 > 0 || current.Item2 == null || current.Item2 == Guid.Empty)
                return (false, string.Empty);
            var currentProfile = PsuPlugin.CustomizeService.GetCustomizeProfileByUniqueId(current.Item2.Value);
            var changed = !string.Equals(currentProfile.Item2, CustomizeState, StringComparison.Ordinal);
            if (!changed)
                return (false, string.Empty);

            if (!string.IsNullOrEmpty(CustomizeState))
            {
                var apply = PsuPlugin.CustomizeService
                    .ApplyTemporaryCustomizeProfileOnCharacter(player.ObjectIndex, CustomizeState);
                if (apply.Item1 > 0)
                {
                    return (false, string.Empty);
                }

                return (true, apply.Item2!.Value.ToString());
            }
            else
            {
                var remove = PsuPlugin.CustomizeService
                    .DeleteTemporaryCustomizeProfileOnCharacter(player.ObjectIndex);
                if (remove > 0)
                    return (false, string.Empty);
                return (true, string.Empty);
            }
        }
        catch
        {
            PsuPlugin.PlayerDataService.ReportMissingPlugin(player.Name.TextValue, "Customize+");
            return (false, string.Empty);
        }
    }
}