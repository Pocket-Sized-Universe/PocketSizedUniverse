using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.ImGuiMethods;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Models.Data;

public class RemotePlayerData(StarPack starPack) : PlayerData(starPack)
{
    internal uint LockKey { get; } = (uint)Random.Shared.Next();
    public sealed override IPlayerCharacter? Player { get; set; }

    public Guid? AssignedCollectionId { get; set; }

    public Guid? AssignedCustomizeProfileId { get; set; }

    public void AddContextMenu(IMenuOpenedArgs args)
    {
        if (Player == null || (args.Target is not MenuTargetDefault target) || target.TargetObject?.GameObjectId != Player.GameObjectId)
            return;
        SeStringBuilder builder = new SeStringBuilder();
        var seString = builder.AddText("Force Apply Data").Build();
        MenuItem menuItem = new MenuItem()
        {
            Name = seString,
            UseDefaultPrefix = false,
            PrefixChar = 'U',
            PrefixColor = 567,
            OnClicked = (a) =>
            {
                if (Player == null)
                    return;
                if (Data == null || GlamourerData == null || PenumbraData == null || HonorificData == null ||
                    CustomizeData == null || MoodlesData == null)
                {
                    Notify.Error("Player data is incomplete, cannot apply.");
                    return;
                }

                GlamourerData.ApplyData(this, true);
                PenumbraData.ApplyData(this, true);
                HonorificData.ApplyData(this, true);
                MoodlesData.ApplyData(this, true);
                CustomizeData.ApplyData(this, true);
                Notify.Success("Data applied.");
            }
        };
        args.AddMenuItem(menuItem);
    }
}