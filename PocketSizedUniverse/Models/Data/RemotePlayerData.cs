using System;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace PocketSizedUniverse.Models.Data;

public class RemotePlayerData(StarPack starPack) : PlayerData(starPack)
{
    internal uint LockKey { get; } = (uint)Random.Shared.Next();
    public sealed override IPlayerCharacter? Player { get; set; }

    public Guid? AssignedCollectionId { get; set; }

    public Guid? AssignedCustomizeProfileId { get; set; }
}
