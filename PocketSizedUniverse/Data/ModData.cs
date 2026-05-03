using Ipfs;

namespace PocketSizedUniverse.Data;

public record CustomAsset
{
    public required Cid Cid { get; set; }
    public required string Extension { get; set; }
    public required List<string> ApplicablePaths { get; set; }
    public virtual bool Equals(CustomAsset? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Cid == other.Cid && ApplicablePaths.SequenceEqual(other.ApplicablePaths);
    }
    public override int GetHashCode() => HashCode.Combine(Cid, ApplicablePaths);
}

public record AssetSwap(string From, string To)
{
    public virtual bool Equals(AssetSwap? other) => other is not null && From == other.From && To == other.To;
    public override int GetHashCode() => HashCode.Combine(From, To);
}