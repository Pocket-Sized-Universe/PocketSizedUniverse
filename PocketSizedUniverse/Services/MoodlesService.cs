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
    internal readonly Action<nint, string> SetStatusManager;

    [EzIPC("GetStatusManagerByPtrV2")]
    internal readonly Func<nint, string> GetStatusManager;
    
    [EzIPC("ClearStatusManagerByPtrV2")]
    internal readonly Action<nint> ClearStatusManager;

    public bool ApplyData(nint address, string status)
    {
        SetStatusManager(address, status);
        return true;
    }
    
    public bool RevertData(nint address)
    {
        ClearStatusManager(address);
        return true;
    }
}