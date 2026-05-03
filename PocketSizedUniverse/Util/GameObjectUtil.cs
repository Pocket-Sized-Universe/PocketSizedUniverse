using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace PocketSizedUniverse.Util;

public static class GameObjectUtil
{
    public static bool IsLocalPlayerRelated(IGameObject? chara, IPlayerCharacter? localPlayer) =>
        chara?.ObjectIndex == localPlayer?.ObjectIndex ||
        chara?.ObjectIndex - 1 == localPlayer?.ObjectIndex ||
        chara?.OwnerId == localPlayer?.OwnerId ||
        chara?.OwnerId == localPlayer?.EntityId;
}