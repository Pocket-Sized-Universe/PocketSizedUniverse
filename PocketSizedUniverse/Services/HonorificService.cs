using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.EzIpcManager;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace PocketSizedUniverse.Services;

public class HonorificService
{
    public HonorificService()
    {
        EzIPC.Init(this, "Honorific");
    }
    [EzIPC("GetLocalCharacterTitle")]
    private readonly Func<string?> _getLocalCharacterTitle;

    [EzIPC("SetCharacterTitle")]
    private readonly Action<int, string> _setCharacterTitle;

    [EzIPC("GetCharacterTitle")]
    private readonly Func<int, string?> _getCharacterTitle;
    
    [EzIPC("ClearCharacterTitle")]
    private readonly Action<int> _clearCharacterTitle;
    
    public string? GetData()
    {
        try
        {
            return _getLocalCharacterTitle();
        }
        catch (IpcNotReadyError)
        {
            return null;
        }
    }
    
    public bool ApplyData(int objectIndex, string title)
    {
        try
        {
            _setCharacterTitle(objectIndex, title);
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
            _clearCharacterTitle(objectIndex);
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
    }
}