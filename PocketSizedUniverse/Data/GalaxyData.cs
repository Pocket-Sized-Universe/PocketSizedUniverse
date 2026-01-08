using Ipfs;

namespace PocketSizedUniverse.Data;

public class GalaxyData
{
    public string GalaxyName { get; set; } = "My New Galaxy!";
    public string Description { get; set; } = "A new galaxy!";
    public List<Cid> CidsToSubscribe { get; set; } = [];
}