using ECommons.EzIpcManager;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

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
    
    public bool ApplyData(string data)
    {
        SetPlayerData(data);
        return true;
    }
}