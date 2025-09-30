using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Style;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Windows;

public class ProgressWindow : Window
{
    public ProgressWindow() : base("PSU Download UI")
    {
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
        const string text = "Sync in Progress...";

        // Get font info once outside the loop
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * 1.2f;
        var textSize = ImGui.CalcTextSize(text) * 1.2f;
        var drawList = ImGui.GetBackgroundDrawList();

        foreach (var remote in PsuPlugin.PlayerDataService.RemotePlayerData)
        {
            if (remote.Value.Player == null)
                continue;
            var rates = PsuPlugin.SyncThingService.GetTransferRates(remote.Value.StarPackReference.StarId);
            bool syncing = rates is { InBps: > 100 };
            if (!syncing)
                continue;
            if (!Svc.GameGui.WorldToScreen(remote.Value.Player.Position, out var screenPos))
                continue;
            if (screenPos == Vector2.Zero)
                continue;

            var centerPos = screenPos with
            {
                X = screenPos.X - textSize.X / 2f,
                Y = screenPos.Y - yOffset
            };

            // Draw shadow for readability
            drawList.AddText(font, fontSize, centerPos + new Vector2(1, 1), UIHelpers.Color(0, 0, 0, shadowTransparency), text);

            // Draw main text
            drawList.AddText(font, fontSize, centerPos, UIHelpers.Color(220, 220, 220, transparency), text);
        }
    }
}