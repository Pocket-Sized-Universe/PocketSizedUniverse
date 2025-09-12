using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using PocketSizedUniverse.Interfaces;
using PocketSizedUniverse.Models.Data;

namespace PocketSizedUniverse.Services;

public class PlayerDataService : IUpdatable
{
    public PlayerDataService()
    {
        Svc.Framework.Update += Update;
    }
    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(10);
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public void Update(IFramework framework)
    {
        if (DateTime.Now - LastUpdated < UpdateInterval) return;
        LastUpdated = DateTime.Now;

        //PushLocalData(); TODO
    }
}