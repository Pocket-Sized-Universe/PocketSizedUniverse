using ECommons.EzIpcManager;

namespace PocketSizedUniverse.Services;

public class SimpleHeelsService
{
    public SimpleHeelsService()
    {
        EzIPC.Init(this, "SimpleHeels");
    }

    [EzIPC("ApiVersion")]
    internal readonly Func<(int, int)> ApiVersion;

    [EzIPC("GetLocalPlayer")]
    internal readonly Func<string> GetLocalPlayer;

    [EzIPC("RegisterPlayer")]
    internal readonly Action<int, string> RegisterPlayer;

    [EzIPC("UnregisterPlayer")]
    internal readonly Action<int> UnregisterPlayer;
}