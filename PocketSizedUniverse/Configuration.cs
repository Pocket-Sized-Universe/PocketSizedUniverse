using System.Collections.Concurrent;
using ECommons.DalamudServices;
using PocketSizedUniverse.Models;

namespace PocketSizedUniverse;

public class Configuration
{
    public string? DefaultDataPackDirectory { get; set; }
    public bool UseBuiltInSyncThing { get; set; } = true;
    public bool SetupComplete { get; set; } = false;
    public bool StarConfigurationComplete { get; set; } = false;
    public string? ApiKey { get; set; }
    public Uri? ApiUri { get; set; }
    public StarPack? MyStarPack { get; set; }
    public List<StarPack> StarPacks { get; set; } = new();
    public List<StarPack> Blocklist { get; set; } = new();
    public ConcurrentDictionary<string, (string GamePath, string RealPath)> TransientFiles { get; set; } = new();
    public int LocalPollingSeconds { get; set; } = 30;
    public int RemotePollingSeconds { get; set; } = 10;
}