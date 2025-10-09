using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.ImGuiMethods;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Models.Data;

public class RemotePlayerData(StarPack starPack) : PlayerData(starPack)
{
    public sealed override IPlayerCharacter? Player { get; set; }

    public Guid? AssignedCollectionId { get; set; }

    public Guid? AssignedCustomizeProfileId { get; set; }
    
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;

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
                PsuPlugin.PlayerDataService.PendingCleanups.Enqueue(StarPackReference.StarId);
                PsuPlugin.PlayerDataService.PendingReads.Enqueue(StarPackReference.StarId);
                Notify.Success("Data application enqueued");
            }
        };
        args.AddMenuItem(menuItem);
    }
}