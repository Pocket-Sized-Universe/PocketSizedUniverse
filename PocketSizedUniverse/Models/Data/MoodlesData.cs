using Dalamud.Game.ClientState.Objects.SubKinds;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class MoodlesData : IDataFile, IEquatable<MoodlesData>
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
    public bool Equals(MoodlesData? obj)
    {
        if (obj == null) return false;
        return MoodlesState == obj.MoodlesState;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);
    public int Version { get; set; } = 1;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;
    public string MoodlesState { get; init; } = string.Empty;

    public (bool Applied, string Result) ApplyData(IPlayerCharacter player, params object[] args)
    {
        try
        {
            var current = PsuPlugin.MoodlesService.GetStatusManager(player.Address);
            var changed = !string.Equals(current, MoodlesState, StringComparison.Ordinal);
            if (!changed)
                return (false, string.Empty);

            if (!string.IsNullOrEmpty(MoodlesState))
            {
                PsuPlugin.MoodlesService.SetStatusManager(player.Address, MoodlesState);
                return (true, "Moodles data applied.");
            }
            else
            {
                PsuPlugin.MoodlesService.ClearStatusManager(player.Address);
                return (true, "Cleared");
            }
        }
        catch (Exception ex)
        {
            PsuPlugin.PlayerDataService.ReportMissingPlugin(player.Name.TextValue, "Moodles");
            return (false, string.Empty);
        }
    }
}
