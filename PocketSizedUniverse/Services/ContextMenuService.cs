using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using ECommons.Configuration;
using ECommons.ImGuiMethods;
using Microsoft.Extensions.Logging;
using PocketSizedUniverse.Config;

namespace PocketSizedUniverse.Services;

public class ContextMenuService : IDisposable
{
    private readonly ILogger<ContextMenuService> _logger;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly PlayerDataService _playerDataService;
    private readonly IContextMenu _context;

    public ContextMenuService(ILogger<ContextMenuService> logger, Configuration configuration, IObjectTable objectTable, PlayerDataService playerDataService, IContextMenu context)
    {
        _context = context;
        _playerDataService = playerDataService;
        _objectTable = objectTable;
        _configuration = configuration;
        _logger = logger;
        _context.OnMenuOpened += AddContextMenu;
    }
    private void AddContextMenu(IMenuOpenedArgs args)
    {
        if (args.MenuType == ContextMenuType.Inventory) return;
        if (args.Target is not MenuTargetDefault target) return;
        var playerData = _playerDataService.PlayerDataByGuid.FirstOrDefault(pd => pd.Value.PlayerData?.EntityId == target.TargetObject?.EntityId);
        if (playerData.Value == null) return;
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
                playerData.Value.Dirty = true;
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
                _configuration.BlockedIds.Add(playerData.Key);
                _configuration.Save();
                _playerDataService.GuidsNeedingRemoval.Enqueue(playerData.Key);
                Notify.Success("Added to blocklist");
            }
        };
        SeStringBuilder cleanupBuilder = new SeStringBuilder();
        var cleanupString = cleanupBuilder.AddText("Cleanup Data").Build();
        MenuItem cleanupMenuItem = new MenuItem()
        {
            Name = cleanupString,
            UseDefaultPrefix = false,
            PrefixChar = 'U',
            PrefixColor = 567,
            OnClicked = (a) =>
            {
                _playerDataService.GuidsNeedingRemoval.Enqueue(playerData.Key);
                Notify.Success("Data cleanup enqueued");
            }
        };
        args.AddMenuItem(menuItem);
        args.AddMenuItem(cleanupMenuItem);
        args.AddMenuItem(banMenuItem);
    }

    public void Dispose()
    {
        _context.OnMenuOpened -= AddContextMenu;
    }
}