using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using PocketSizedUniverse.Config;
using PocketSizedUniverse.Controllers;
using PocketSizedUniverse.Data;

namespace PocketSizedUniverse.GUI;

public class ChatWindow : Window
{
    private readonly ChatController _chatController;
    private readonly Configuration _configuration;
    private readonly Guid _chatId;
    private readonly WindowSystem _windowSystem;
    private string _messageInput = string.Empty;
    private bool _scrollToBottom = false;

    public ChatWindow(Guid chatId, ChatController chatController, Configuration configuration, WindowSystem windowSystem) : base($"Chat - {configuration.Nicknames.GetValueOrDefault(chatId, chatId.ToString())}##{chatId}")
    {
        _chatId = chatId;
        _chatController = chatController;
        _configuration = configuration;
        _windowSystem = windowSystem;
        Size = new Vector2(400, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    
    public List<ChatMessage> Messages => _chatController.ChatMessages.TryGetValue(_chatId, out var chatMessages) ? chatMessages.Values.OrderBy(m => m.Timestamp).ToList() : new List<ChatMessage>();

    public override void Draw()
    {
        DrawMessageList();
        ImGui.Separator();
        DrawInputArea();
    }

    public override void OnClose()
    {
        _windowSystem.RemoveWindow(this);
        base.OnClose();
    }

    private void DrawMessageList()
    {
        var footerHeightToReserve = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing() * 3; // Space for multiline input
        using var child = ImRaii.Child("ScrollingRegion", new Vector2(0, -footerHeightToReserve), false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!child) return;

        var messages = Messages;
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            DrawMessage(message, i % 2 == 0);
        }

        if (_scrollToBottom || (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() && messages.Count > 0))
        {
            ImGui.SetScrollHereY(1.0f);
            _scrollToBottom = false;
        }
    }

    private void DrawMessage(ChatMessage message, bool alternate)
    {
        var senderNickname = message.SenderId == _configuration.PairingId ? "Me" : _configuration.Nicknames.GetValueOrDefault(message.SenderId, message.SenderId.ToString());
        var timestamp = message.Timestamp.ToLocalTime().ToString("g");

        // Subtle background for alternating messages
        var bgColor = alternate ? ImGui.GetColorU32(ImGuiCol.FrameBg, 0.4f) : ImGui.GetColorU32(ImGuiCol.FrameBg, 0.15f);
        
        var startPos = ImGui.GetCursorScreenPos();
        var windowWidth = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();

        // Use channels to draw background behind text
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1); // Foreground channel
        
        using (ImRaii.Group())
        {
            // Add some padding from the rectangle edge
            ImGui.SetCursorScreenPos(startPos + new Vector2(5, 5));
            
            using (ImRaii.Group())
            {
                ImGui.TextDisabled($"{senderNickname} ({timestamp})");
                ImGui.Separator();
                ImGui.PushTextWrapPos(0.0f);
                ImGui.TextUnformatted(message.Message);
                ImGui.PopTextWrapPos();
            }
            
            // Add bottom padding for the group
            ImGui.Dummy(new Vector2(0, 5));
        }
        
        var endPos = ImGui.GetItemRectMax();
        
        // Draw background rectangle in the background channel
        drawList.ChannelsSetCurrent(0); // Background channel
        drawList.AddRectFilled(startPos, new Vector2(startPos.X + windowWidth, endPos.Y), bgColor, 4.0f);
        
        drawList.ChannelsMerge();
        
        // Ensure cursor is moved to the end of this message block
        ImGui.SetCursorScreenPos(new Vector2(startPos.X, endPos.Y + 10));
    }

    private void DrawInputArea()
    {
        var buttonWidth = 60f;
        var inputWidth = ImGui.GetContentRegionAvail().X - buttonWidth - ImGui.GetStyle().ItemSpacing.X;

        ImGui.SetNextItemWidth(inputWidth);
        if (ImGui.InputTextMultiline("##ChatInput", ref _messageInput, 5000, new Vector2(inputWidth, ImGui.GetFrameHeight() * 3), ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CtrlEnterForNewLine))
        {
            SendMessage();
        }

        ImGui.SameLine();

        if (ImGui.Button("Send", new Vector2(buttonWidth, ImGui.GetFrameHeight() * 3)))
        {
            SendMessage();
        }
    }

    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(_messageInput)) return;

        Task.Run(async () =>
        {
            await _chatController.SendChatMessage(_chatId, _messageInput);
            _messageInput = string.Empty;
            _scrollToBottom = true;
        });
    }
}