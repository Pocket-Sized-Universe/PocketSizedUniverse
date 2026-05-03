using Dalamud.Plugin.Ipc.Exceptions;
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
    private readonly Func<string> _getPlayerData;
    
    [EzIPC("SetPlayerData")]
    private readonly Action<string> _setPlayerData;
    
    public string? GetData()
    {
        try
        {
            return _getPlayerData();
        }
        catch (IpcNotReadyError)
        {
            return null;
        }
    }
    
    public bool ApplyData(string data)
    {
        try
        {
            _setPlayerData(data);
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
    }
}