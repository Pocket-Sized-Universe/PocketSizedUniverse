using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.EzIpcManager;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace PocketSizedUniverse.Services;

public class MoodlesService
{
    public MoodlesService()
    {
        EzIPC.Init(this, "Moodles");
    }
    [EzIPC("SetStatusManagerByPtrV2")]
    private readonly Action<nint, string> _setStatusManager;

    [EzIPC("GetStatusManagerByPtrV2")]
    private readonly Func<nint, string> _getStatusManager;
    
    [EzIPC("ClearStatusManagerByPtrV2")]
    private readonly Action<nint> _clearStatusManager;
    
    public string? GetData(nint address)
    {
        try
        {
            return _getStatusManager(address);
        }
        catch (IpcNotReadyError)
        {
            return null;
        }
    }

    public bool ApplyData(nint address, string status)
    {
        try
        {
            _setStatusManager(address, status);
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
    }
    
    public bool RevertData(nint address)
    {
        try
        {
            _clearStatusManager(address);
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
    }
}