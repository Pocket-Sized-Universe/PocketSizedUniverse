using ECommons.DalamudServices;
using PocketSizedUniverse.Models;

namespace PocketSizedUniverse;

public class Configuration
{
    public string SyncThingHomePath { get; set; } = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "engine-home");
    public string? DefaultDataPackDirectory { get; set; }
    public bool UseBuiltInSyncThing { get; set; } = false;
    public bool SetupComplete { get; set; } = false;
    public bool StarConfigurationComplete { get; set; } = false;
    public string? ApiKey { get; set; }
    public Uri? ApiUri { get; set; }
    public StarPack? MyStarPack { get; set; }
    public List<StarPack> StarPacks { get; set; } = new();
}