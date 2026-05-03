using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc.Exceptions;
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
    private readonly Func<Guid, (int, string?)> _getCustomizeProfileByUniqueId;

    [EzIPC("Profile.SetTemporaryProfileOnCharacter")]
    private readonly Func<int, string, (int, Guid?)> _applyTemporaryCustomizeProfileOnCharacter;

    [EzIPC("Profile.GetActiveProfileIdOnCharacter")]
    private readonly Func<int, (int, Guid?)> _getActiveProfileOnCharacter;

    [EzIPC("Profile.DeleteTemporaryProfileOnCharacter")]
    private readonly Func<ushort, int> _deleteTemporaryCustomizeProfileOnCharacter;

    public string? GetData(int objectIndex)
    {
        try
        {
            var result = _getActiveProfileOnCharacter(objectIndex);
            if (result.Item1 != 0 || result.Item2 == null) return null;
            var profileDataResult = _getCustomizeProfileByUniqueId(result.Item2.Value);
            if (profileDataResult.Item1 != 0 || profileDataResult.Item2 == null) return null;
            return profileDataResult.Item2;
        }
        catch (IpcNotReadyError)
        {
            return null;
        }
    }
    
    public bool ApplyData(int objectIndex, string customizeData)
    {
        try
        {
            var result = _applyTemporaryCustomizeProfileOnCharacter(objectIndex, customizeData);
            return result.Item1 == 0;
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
            var result = _deleteTemporaryCustomizeProfileOnCharacter((ushort)objectIndex);
            return result == 0;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
    }
}