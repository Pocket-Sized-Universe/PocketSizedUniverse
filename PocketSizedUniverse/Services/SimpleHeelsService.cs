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
    internal readonly Func<(int, int)> ApiVersion;

    [EzIPC("GetLocalPlayer")]
    internal readonly Func<string> GetLocalPlayer;

    [EzIPC("RegisterPlayer")]
    internal readonly Action<int, string> RegisterPlayer;

    [EzIPC("UnregisterPlayer")]
    internal readonly Action<int> UnregisterPlayer;

    public bool ApplyData(int objectIndex, string playerName)
    {
        RegisterPlayer(objectIndex, playerName);
        return true;
    }
}