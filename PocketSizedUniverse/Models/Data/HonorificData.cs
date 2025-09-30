using Dalamud.Game.ClientState.Objects.SubKinds;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Data;

public class HonorificData : IDataFile, IEquatable<HonorificData>
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
    public bool Equals(HonorificData? obj)
    {
        if (obj == null) return false;
        return Title == obj.Title;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public static string Filename { get; } = "Honorific.dat";
    public string GetPath(string basePath) => Path.Combine(basePath, Filename);
    public string Title { get; init; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

    public (bool Applied, string Result) ApplyData(IPlayerCharacter player, params object[] args)
    {
        var current = PsuPlugin.HonorificService.GetCharacterTitle(player.ObjectIndex);
        var changed = !string.Equals(current, Title, StringComparison.Ordinal);
        if (!changed)
            return (false, string.Empty);

        if (!string.IsNullOrEmpty(Title))
        {
            PsuPlugin.HonorificService.SetCharacterTitle(player.ObjectIndex, Title);
            return (true, Title);
        }
        else
        {
            PsuPlugin.HonorificService.ClearCharacterTitle(player.ObjectIndex);
            return (true, "Cleared");
        }
    }
}
