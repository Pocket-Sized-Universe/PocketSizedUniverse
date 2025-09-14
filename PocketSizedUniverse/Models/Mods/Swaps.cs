using Newtonsoft.Json;
using PocketSizedUniverse.Interfaces;

namespace PocketSizedUniverse.Models.Mods;

public interface IAssetSwap
{
    public string? GamePath { get; }
}

public record AssetSwap(string? GamePath, string RealPath): IAssetSwap;

public record CustomRedirect(byte[] Hash): IWriteableData
{
    public List<string> ApplicableGamePaths { get; set; } = new();
    [JsonIgnore]
    public string FileName => $"{Convert.ToBase64String(Hash).Replace("/", "_").Replace("+", "-")}.psu_mod";
    public bool Equals(IWriteableData? x, IWriteableData? y)
    {
        return ReferenceEquals(x, y);
    }

    public int GetHashCode(IWriteableData obj)
    {
        return obj.Id.GetHashCode();   
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string GetPath(string basePath) => Path.Combine(basePath, FileName);
}