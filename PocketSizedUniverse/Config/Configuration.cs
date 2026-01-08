using System.Collections.Concurrent;
using Dalamud.Configuration;
using Ipfs;
using Newtonsoft.Json;
using PocketSizedUniverse.Data;

namespace PocketSizedUniverse.Config;

public partial class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public Guid PairingId { get; set; } = Guid.Empty;
    public ConcurrentDictionary<string, List<string>> TransientFilesData { get; set; } = new();
    public List<Guid> IndividualPairs { get; set; } = [];
    public List<Cid> SubscribedGalaxies { get; set; } = [];
    public ConcurrentDictionary<Cid, GalaxyData> ControlledGalaxies { get; set; } = new();
    public string? CacheDirectory { get; set; } = null;

    public int MaxParallelIpfsRequests { get; set; } = 3;
    public IpfsModeType? IpfsMode { get; set; }
    public bool SetRecommendedIpfsConfigs { get; set; } = true;
    public string? IpfsApiUrl { get; set; }
    [JsonIgnore] public bool Dirty = false;
    public enum IpfsModeType
    {
        Easy,
        Expert
    }
}