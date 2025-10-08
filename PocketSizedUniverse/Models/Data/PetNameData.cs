using Dalamud.Game.ClientState.Objects.SubKinds;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class PetNameData : IDataFile, IEquatable<PetNameData>
{
    public static PetNameData? LoadFromDisk(string basePath)
    {
        var path = Path.Combine(basePath, Filename);
        if (!File.Exists(path))
        {
            return null;
        }

        var data = File.ReadAllText(path);
        return Base64Util.FromBase64<PetNameData>(data);
    }

    public static string Filename => "PetName.dat";
    public Guid Id { get; set; } = Guid.NewGuid();

    public string GetPath(string basePath) => Path.Combine(basePath, Filename);

    public int Version { get; set; } = 1;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;
    public string PetName { get; init; } = string.Empty;

    public (bool Applied, string Result) ApplyData(IPlayerCharacter player, params object[] args)
    {
        try
        {
            PsuPlugin.PetNameService.SetPlayerData(PetName);
            return (true, "Pet name data applied.");
        }
        catch (Exception ex)
        {
            PsuPlugin.PlayerDataService.ReportMissingPlugin(player.Name.TextValue, "Pet Nicknames");
            return (false, ex.Message);
        }
    }

    public bool Equals(PetNameData? other)
    {
        if (other == null) return false;
        return PetName == other.PetName;
    }
}