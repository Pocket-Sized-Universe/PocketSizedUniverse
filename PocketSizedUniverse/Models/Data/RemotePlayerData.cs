using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.Configuration;
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
        SeStringBuilder banBuilder = new SeStringBuilder();
        var banString = banBuilder.AddText("Add to Blocklist").Build();
        MenuItem banMenuItem = new MenuItem()
        {
            Name = banString,
            UseDefaultPrefix = false,
            PrefixChar = 'U',
            PrefixColor = 567,
            OnClicked = (a) =>
            {
                PsuPlugin.Configuration.Blocklist.Add(StarPackReference);
                EzConfig.Save();
                PsuPlugin.PlayerDataService.PendingCleanups.Enqueue(StarPackReference.StarId);
                Notify.Success("Added to blocklist");
            }
        };
        args.AddMenuItem(menuItem);
        args.AddMenuItem(banMenuItem);
    }
}