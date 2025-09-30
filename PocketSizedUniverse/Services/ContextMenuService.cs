using Dalamud.Game.Gui.ContextMenu;
using ECommons.DalamudServices;

namespace PocketSizedUniverse.Services;

public class ContextMenuService
{
    public ContextMenuService()
    {
        Svc.ContextMenu.OnMenuOpened += ContextMenuOnOnMenuOpened;
    }

    private void ContextMenuOnOnMenuOpened(IMenuOpenedArgs args)
    {
        if (args.MenuType is ContextMenuType.Inventory) return;
        foreach (var remote in PsuPlugin.PlayerDataService.RemotePlayerData)
        {
            remote.Value.AddContextMenu(args);
        }
    }
}