using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.EzIpcManager;

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
}