using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.EzIpcManager;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace PocketSizedUniverse.Services;

public class SimpleHeelsService
{
    public SimpleHeelsService()
    {
        EzIPC.Init(this, "SimpleHeels");
    }

    [EzIPC("ApiVersion")]
    private readonly Func<(int, int)> _apiVersion;

    [EzIPC("GetLocalPlayer")]
    private readonly Func<string> _getLocalPlayer;

    [EzIPC("RegisterPlayer")]
    private readonly Action<int, string> _registerPlayer;

    [EzIPC("UnregisterPlayer")]
    private readonly Action<int> _unregisterPlayer;

    public string? GetData()
    {
        try
        {
            return _getLocalPlayer();
        }
        catch (IpcNotReadyError)
        {
            return null;
        }
    }

    public bool ApplyData(int objectIndex, string playerName)
    {
        try{
            _registerPlayer(objectIndex, playerName);
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
    }
    
    public bool RevertData(int objectIndex)
    {
        try
        {
            _unregisterPlayer(objectIndex);
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
    }
}