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

    // Preserve original file extension so Penumbra can infer type correctly (e.g., .mdl/.tex/.mtrl/etc.)
    public string? FileExtension { get; set; }

    [JsonIgnore]
    public string FileName
    {
        get
        {
            var baseName = Convert.ToBase64String(Hash).Replace("/", "_").Replace("+", "-");
            var ext = string.IsNullOrWhiteSpace(FileExtension) ? ".psu_mod" : (FileExtension!.StartsWith('.') ? FileExtension! : "." + FileExtension!);
            return baseName + ext;
        }
    }

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
