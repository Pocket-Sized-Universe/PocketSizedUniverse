using Dalamud.Game.ClientState.Objects.SubKinds;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class HeelsData : IDataFile, IEquatable<HeelsData>
{
    public static HeelsData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<HeelsData>(data);
    }
    public static string Filename { get; } = "Heels.dat";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);

    public int Version { get; set; } = 1;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;
    public (bool Applied, string Result) ApplyData(IPlayerCharacter player, params object[] args)
    {
        try
        {
            if (!string.IsNullOrEmpty(HeelsState))
            {
                PsuPlugin.SimpleHeelsService.RegisterPlayer(player.ObjectIndex, HeelsState);
                return (true, "Heels data applied.");
            }
            else
            {
                PsuPlugin.SimpleHeelsService.UnregisterPlayer(player.ObjectIndex);
                return (true, "Cleared");
            }
        }
        catch
        {
            PsuPlugin.PlayerDataService.ReportMissingPlugin(player.Name.TextValue, "SimpleHeels");
            return (false, string.Empty);
        }
    }

    public string HeelsState { get; init; } = string.Empty;
    public bool Equals(HeelsData? other)
    {
        if (other == null) return false;
        return HeelsState == other.HeelsState;
    }
}