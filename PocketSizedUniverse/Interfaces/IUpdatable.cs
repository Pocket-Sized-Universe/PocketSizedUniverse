using Dalamud.Plugin.Services;

namespace PocketSizedUniverse.Interfaces;

public interface IUpdatable
{
    public TimeSpan UpdateInterval { get; set; }
    public DateTime LastUpdated { get; set; }
    public void LocalUpdate(IFramework framework);
}