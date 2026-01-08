using Ipfs;

namespace PocketSizedUniverse.Data;

public class RemoteData
{
    public bool Dirty { get; set; } = false;
    public Guid PairId { get; set; }
    public PlayerData? PlayerData { get; set; }
    public Guid? CollectionId { get; set; }
    public Dictionary<string, string>? PreparedPaths { get; set; }
}