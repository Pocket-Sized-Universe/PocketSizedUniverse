using ECommons.EzIpcManager;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace PocketSizedUniverse.Services;

public class CustomizeService
{
    public CustomizeService()
    {
        EzIPC.Init(this, "CustomizePlus");
    }
    [EzIPC("Profile.GetByUniqueId")]
    internal readonly Func<Guid, (int, string?)> GetCustomizeProfileByUniqueId;

    [EzIPC("Profile.SetTemporaryProfileOnCharacter")]
    internal readonly Func<int, string, (int, Guid?)> ApplyTemporaryCustomizeProfileOnCharacter;

    [EzIPC("Profile.GetActiveProfileIdOnCharacter")]
    internal readonly Func<int, (int, Guid?)> GetActiveProfileOnCharacter;

    [EzIPC("Profile.DeleteTemporaryProfileOnCharacter")]
    internal readonly Func<ushort, int> DeleteTemporaryCustomizeProfileOnCharacter;
}