using Penumbra.Api.Helpers;

namespace PocketSizedUniverse.Models.Mods;

public class SyncedMod
{
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public Dictionary<string, List<string>> Settings { get; set; } = new();
    public bool Inherited { get; set; } = false;
    public bool Temporary { get; set; } = false;
    public List<CustomRedirect> CustomFiles { get; set; } = new();
}