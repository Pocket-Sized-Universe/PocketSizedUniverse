using ECommons.EzIpcManager;

namespace PocketSizedUniverse.Services;

public class PetNameService
{
    public PetNameService()
    {
        EzIPC.Init(this, "PetRenamer");
    }
    [EzIPC("GetPlayerData")]
    internal readonly Func<string> GetPlayerData;
    
    [EzIPC("SetPlayerData")]
    internal readonly Action<string> SetPlayerData;
}