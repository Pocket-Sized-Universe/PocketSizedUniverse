using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using PocketSizedUniverse.Controllers;
using PocketSizedUniverse.Services;
using PocketSizedUniverse.Util;

namespace PocketSizedUniverse.GUI;

public class OverlayWindow : Window
{
    private readonly PlayerDataService _playerDataService;
    private readonly IObjectTable _objectTable;
    private readonly ModController _modController;
    private readonly IGameGui _gameGui;

    public OverlayWindow(PlayerDataService playerDataService, IObjectTable objectTable, ModController modController, IGameGui gameGui) : base("PSU Download UI")
    {
        _playerDataService = playerDataService;
        _modController = modController;
        _objectTable = objectTable;
        _gameGui = gameGui;
        
        SizeConstraints = new WindowSizeConstraints()
        {
            MaximumSize = new Vector2(500, 90),
            MinimumSize = new Vector2(500, 90),
        };

        Flags |= ImGuiWindowFlags.NoMove;
        Flags |= ImGuiWindowFlags.NoBackground;
        Flags |= ImGuiWindowFlags.NoInputs;
        Flags |= ImGuiWindowFlags.NoNavFocus;
        Flags |= ImGuiWindowFlags.NoResize;
        Flags |= ImGuiWindowFlags.NoScrollbar;
        Flags |= ImGuiWindowFlags.NoTitleBar;
        Flags |= ImGuiWindowFlags.NoDecoration;
        Flags |= ImGuiWindowFlags.NoFocusOnAppearing;

        DisableWindowSounds = true;

        ForceMainWindow = true;

        IsOpen = true;
    }

    public override void Draw()
    {
        const int transparency = 220;
        const int shadowTransparency = 150;
        const float yOffset = 65f;

        // Get font info once outside the loop
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * 1.2f;
        
        var drawList = ImGui.GetBackgroundDrawList();

        foreach (var remote in _playerDataService.PlayerDataByGuid)
        {
            var playerId = remote.Value.PlayerData?.EntityId;
            if (playerId == null)
                continue;
            var playerObj = _objectTable.PlayerObjects.FirstOrDefault(p => p.EntityId == playerId);
            if (playerObj == null)
                continue;
            var filesInFlight = _modController.FileResolveTasks.Count(frt => (remote.Value.PlayerData?.ModFiles.Any(f => frt.Key == f.Cid) ?? false) && !frt.Value.IsCompleted);
            if (filesInFlight == 0)
                continue;
            var text = $"Downloading {filesInFlight} files...";
            var textSize = ImGui.CalcTextSize(text) * 1.2f;
            if (!_gameGui.WorldToScreen(playerObj.Position, out var screenPos))
                continue;
            if (screenPos == Vector2.Zero)
                continue;

            var centerPos = screenPos with
            {
                X = screenPos.X - textSize.X / 2f,
                Y = screenPos.Y - yOffset
            };

            //drawList.AddRectFilled(centerPos, centerPos + textSize, GuiUtils.Color(0, 0, 0, transparency));
            // Draw shadow for readability
            drawList.AddText(font, fontSize, centerPos + new Vector2(1, 1), GuiUtils.Color(0, 0, 0, shadowTransparency), text);

            // Draw main text
            drawList.AddText(font, fontSize, centerPos, GuiUtils.Color(255, 255, 255, transparency), text);
        }
    }
}