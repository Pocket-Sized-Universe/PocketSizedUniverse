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
    internal readonly Func<string?> GetLocalCharacterTitle;

    [EzIPC("SetCharacterTitle")]
    internal readonly Action<int, string> SetCharacterTitle;

    [EzIPC("GetCharacterTitle")]
    internal readonly Func<int, string?> GetCharacterTitle;
    
    [EzIPC("ClearCharacterTitle")]
    internal readonly Action<int> ClearCharacterTitle;
    
    public bool ApplyData(int objectIndex, string title)
    {
        SetCharacterTitle(objectIndex, title);
        return true;
    }
    
    public bool RevertData(int objectIndex)
    {
        ClearCharacterTitle(objectIndex);
        return true;
    }
}