using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class BasicData : IDataFile, IEquatable<BasicData>
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

    [JsonRequired] public string PlayerName { get; set; } = string.Empty;
    public uint WorldId { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

    public bool Equals(BasicData? obj)
    {
        if (obj == null) return false;
        return PlayerName == obj.PlayerName && WorldId == obj.WorldId;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);

    public (bool Applied, string Result) ApplyData(IPlayerCharacter player, params object[] args)
    {
        // BasicData has no in-game representation to apply.
        return (false, string.Empty);
    }
}
