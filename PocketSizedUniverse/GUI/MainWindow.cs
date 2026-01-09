using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using HaselCommon.Gui;
using Ipfs;
using PocketSizedUniverse.Controllers;
using PocketSizedUniverse.Data;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse.GUI;

public class MainWindow : Window
{
    private readonly ModController _modController;
    private readonly DataController _dataController;
    private readonly Config.Configuration _configuration;
    private readonly IpfsService _ipfsService;
    private readonly ChatController _chatController;
    private readonly WindowSystem _windowSystem;

    private const float FixedWindowWidth = 400f;
    private const int TruncatedCodeLength = 12;
    private const float HeaderFontScale = 1.3f;
    private const float TabFontScale = 1.15f;

    public MainWindow(ModController modController, DataController dataController, Config.Configuration configuration,
        IpfsService ipfsService, ChatController chatController, WindowSystem windowSystem) : base(
        "Pocket Sized Universe", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _configuration = configuration;
        _modController = modController;
        _dataController = dataController;
        _ipfsService = ipfsService;
        _chatController = chatController;
        _windowSystem = windowSystem;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(FixedWindowWidth, 400),
            MaximumSize = new Vector2(FixedWindowWidth, 4000)
        };
    }

    enum DrawState
    {
        Stars,
        Galaxies,
        Chats
    }

    DrawState _drawState = DrawState.Stars;

    public override void Draw()
    {
        DrawUserInfoSection();
        ImGuiUtils.DrawPaddedSeparator();
        DrawTabSelector();
        ImGuiUtils.DrawPaddedSeparator();
        DrawDataSection();
    }

    private void DrawUserInfoSection()
    {
        DrawCenteredScaledText("Your Pairing Code", HeaderFontScale);
        ImGui.Spacing();

        var cid = _configuration.PairingId;
        var fullCode = cid.ToString();

        if (DrawCenteredSelectable(fullCode, ImGuiSelectableFlags.None))
        {
            ImGui.SetClipboardText(fullCode);
            Notify.Success("Pairing code copied to clipboard!");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(fullCode);
            ImGui.EndTooltip();
        }
    }

    private void DrawTabSelector()
    {
        var windowWidth = ImGui.GetContentRegionAvail().X;
        var tabWidth = windowWidth / 3f;

        DrawTab("Stars", DrawState.Stars, tabWidth, true);
        ImGui.SameLine(0, 0);
        DrawTab("Galaxies", DrawState.Galaxies, tabWidth, true);
        ImGui.SameLine(0, 0);
        DrawTab("Chats", DrawState.Chats, tabWidth, false);
    }

    private enum GalaxyDrawState
    {
        Subscribed,
        Managed
    }

    private GalaxyDrawState _galaxyDrawState = GalaxyDrawState.Subscribed;

    private void DrawGalaxyTabSelector()
    {
        var windowWidth = ImGui.GetContentRegionAvail().X;
        var tabWidth = windowWidth / 2f;

        DrawGalaxyTab("Subscribed", GalaxyDrawState.Subscribed, tabWidth, true);
        ImGui.SameLine(0, 0);
        DrawGalaxyTab("Managed", GalaxyDrawState.Managed, tabWidth, false);
    }

    private void DrawGalaxyTab(string label, GalaxyDrawState state, float width, bool drawSeparator)
    {
        var isActive = _galaxyDrawState == state;
        var colors = new Dictionary<ImGuiCol, uint>();

        if (isActive)
        {
            colors[ImGuiCol.Header] = ImGui.GetColorU32(ImGuiCol.ButtonActive);
            colors[ImGuiCol.HeaderHovered] = ImGui.GetColorU32(ImGuiCol.ButtonActive);
        }
        else
        {
            colors[ImGuiCol.Header] = ImGui.GetColorU32(ImGuiCol.FrameBg);
            colors[ImGuiCol.HeaderHovered] = ImGui.GetColorU32(ImGuiCol.FrameBgHovered);
        }

        foreach (var (col, colorVal) in colors)
            ImGui.PushStyleColor(col, colorVal);

        ImGui.PushFont(ImGui.GetFont());
        ImGui.SetWindowFontScale(TabFontScale);
        var scaledTextSize = ImGui.CalcTextSize(label);
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopFont();

        var clicked = ImGui.Selectable($"##tab_{label}", isActive, ImGuiSelectableFlags.None, new Vector2(width, 0));

        var offset = (width - scaledTextSize.X) / 2f;
        var itemMin = ImGui.GetItemRectMin();
        var textPos = new Vector2(itemMin.X + offset, itemMin.Y);

        ImGui.PushFont(ImGui.GetFont());
        ImGui.SetWindowFontScale(TabFontScale);
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), textPos,
            ImGui.GetColorU32(ImGuiCol.Text), label);
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopFont();

        if (clicked)
        {
            _galaxyDrawState = state;
        }

        foreach (var _ in colors)
            ImGui.PopStyleColor();

        if (drawSeparator)
        {
            var pos = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(
                new Vector2(pos.X, pos.Y - ImGui.GetItemRectSize().Y),
                pos,
                ImGui.GetColorU32(ImGuiCol.Separator),
                2f
            );
        }
    }

    private void DrawTab(string label, DrawState state, float width, bool drawSeparator)
    {
        var isActive = _drawState == state;
        var colors = new Dictionary<ImGuiCol, uint>();

        if (isActive)
        {
            colors[ImGuiCol.Header] = ImGui.GetColorU32(ImGuiCol.ButtonActive);
            colors[ImGuiCol.HeaderHovered] = ImGui.GetColorU32(ImGuiCol.ButtonActive);
        }
        else
        {
            colors[ImGuiCol.Header] = ImGui.GetColorU32(ImGuiCol.FrameBg);
            colors[ImGuiCol.HeaderHovered] = ImGui.GetColorU32(ImGuiCol.FrameBgHovered);
        }

        foreach (var (col, colorVal) in colors)
            ImGui.PushStyleColor(col, colorVal);

        ImGui.PushFont(ImGui.GetFont());
        ImGui.SetWindowFontScale(TabFontScale);
        var scaledTextSize = ImGui.CalcTextSize(label);
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopFont();

        var clicked = ImGui.Selectable($"##tab_{label}", isActive, ImGuiSelectableFlags.None, new Vector2(width, 0));

        var offset = (width - scaledTextSize.X) / 2f;
        var itemMin = ImGui.GetItemRectMin();
        var textPos = new Vector2(itemMin.X + offset, itemMin.Y);

        ImGui.PushFont(ImGui.GetFont());
        ImGui.SetWindowFontScale(TabFontScale);
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), textPos,
            ImGui.GetColorU32(ImGuiCol.Text), label);
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopFont();

        if (clicked)
        {
            _drawState = state;
        }

        foreach (var _ in colors)
            ImGui.PopStyleColor();

        if (drawSeparator)
        {
            var pos = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(
                new Vector2(pos.X, pos.Y - ImGui.GetItemRectSize().Y),
                pos,
                ImGui.GetColorU32(ImGuiCol.Separator),
                2f
            );
        }
    }

    private void DrawDataSection()
    {
        switch (_drawState)
        {
            case DrawState.Stars:
                DrawStars();
                break;
            case DrawState.Galaxies:
                DrawGalaxies();
                break;
            case DrawState.Chats:
                DrawChats();
                break;
        }
    }

    private void DrawChats()
    {
        var buttonWidth = 200f;
        ImGui.Spacing();
        
        DrawCenteredButton("Create Chat", buttonWidth, CreateChat);
        ImGui.Spacing();
        DrawCenteredButton("Join Chat", buttonWidth, JoinChat);
        
        ImGui.Spacing();
        ImGui.Spacing();

        var chats = _configuration.Chats;
        if (chats.Count > 0)
        {
            DrawGuidList(chats, OpenChatWindow);
        }
        else
        {
            DrawCenteredText("You haven't joined any chats.");
        }
    }

    private void JoinChat()
    {
        var text = ImGui.GetClipboardText();
        if (Guid.TryParse(text, out var chatId))
        {
            _configuration.Chats.Add(chatId);
            _configuration.Save();
            Notify.Success("Chat joined successfully!");
        }
        else
        {
            Notify.Error("Invalid chat ID!");
        }
    }

    private void CreateChat()
    {
        var chatId = Guid.NewGuid();
        _configuration.Chats.Add(chatId);
        _configuration.Save();
        Notify.Success("Chat created successfully!");
    }

    private void DrawGalaxies()
    {
        var buttonWidth = 200f;
        ImGui.Spacing();

        DrawCenteredButton("Create Galaxy", buttonWidth, CreateGalaxy);
        ImGui.Spacing();
        DrawCenteredButton("Join Galaxy", buttonWidth, JoinGalaxy);
        
        ImGui.Spacing();
        ImGui.Spacing();

        var galaxies = _configuration.Galaxies;
        if (galaxies.Count > 0)
        {
            DrawGuidList(galaxies, CopyGuidToClipboard);
        }
        else
        {
            ImGui.Spacing();
            DrawCenteredText("You're not in any Galaxies.");
        }
    }

    private void CopyGuidToClipboard(Guid guid)
    {
        ImGui.SetClipboardText(guid.ToString());
        Notify.Success("Code copied to clipboard!");
    }

    private void OpenChatWindow(Guid chatId)
    {
        if (_windowSystem.Windows.Any(w => w is ChatWindow cw && cw.ChatId == chatId))
            return;
        var chatWindow = new ChatWindow(chatId, _chatController, _configuration, _windowSystem);
        _windowSystem.AddWindow(chatWindow);
        chatWindow.IsOpen = true;
    }

    private void JoinGalaxy()
    {
        var text = ImGui.GetClipboardText();
        if (Guid.TryParse(text, out var guid))
        {
            _configuration.Galaxies.Add(guid);
            _configuration.Save();
            Notify.Success("Galaxy joined successfully!");
        }
        else
        {
            Notify.Error("Invalid galaxy ID!");
        }
    }

    private void CreateGalaxy()
    {
        var galaxyId = Guid.NewGuid();
        _configuration.Galaxies.Add(galaxyId);
        _configuration.Save();
        Notify.Success("Galaxy created successfully!");
    }

    public void DrawStars()
    {
        var buttonWidth = 200f;
        ImGui.Spacing();

        DrawCenteredButton("Pair New Code", buttonWidth, PairNewCode);

        ImGui.Spacing();
        ImGui.Spacing();

        var pairs = _configuration.IndividualPairs;
        if (pairs.Count > 0)
        {
            DrawGuidList(pairs, CopyGuidToClipboard);
        }
        else
        {
            ImGui.Spacing();
            DrawCenteredText("You're not paired with anyone.");
        }
    }
    
    private string _newNickName = "";
    private string _newNote = "";
    private void DrawGuidList(List<Guid> guids, Action<Guid> onClick)
    {
        var cidToRemove = (Guid?)null;
        var openNicknamePopup = (Guid?)null;
        var openNotePopup = (Guid?)null;

        foreach (var guid in guids)
        {
            var displayText = _configuration.Nicknames.TryGetValue(guid, out var nickname) ? nickname : guid.ToString();

            var windowWidth = ImGui.GetContentRegionAvail().X;
            var clicked = ImGui.Selectable($"##star_{guid}", false, ImGuiSelectableFlags.None,
                new Vector2(windowWidth, 0));

            if (clicked)
                onClick(guid);

            if (ImGui.IsItemHovered() && _configuration.Notes.TryGetValue(guid, out var note))
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextWrapped(note);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }

            var textSize = ImGui.CalcTextSize(displayText);
            var offset = (windowWidth - textSize.X) / 2f;
            var itemMin = ImGui.GetItemRectMin();
            var textPos = new Vector2(itemMin.X + offset, itemMin.Y);

            ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), displayText);

            if (ImGui.BeginPopupContextItem($"##star_context_{guid}"))
            {
                if (ImGui.MenuItem("Copy Code"))
                {
                    ImGui.SetClipboardText(guid.ToString());
                    Notify.Success("Pairing code copied to clipboard!");
                }

                if (ImGui.MenuItem("Remove"))
                {
                    cidToRemove = guid;
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Edit Nickname"))
                {
                    _newNickName = displayText;
                    openNicknamePopup = guid;
                }

                if (ImGui.MenuItem("Edit Note"))
                {
                    _newNote = _configuration.Notes.GetValueOrDefault(guid, "");
                    openNotePopup = guid;
                }

                ImGui.EndPopup();
            }

            if (openNicknamePopup == guid)
            {
                ImGui.OpenPopup($"##star_nickname_{guid}");
            }

            if (openNotePopup == guid)
            {
                ImGui.OpenPopup($"##star_note_{guid}");
            }

            if (ImGui.BeginPopup($"##star_nickname_{guid}"))
            {
                ImGui.Text("Nickname:");
                ImGui.InputText("##nickname", ref _newNickName, 128);
                ImGui.Spacing();
                if (ImGui.Button("Save"))
                {
                    _configuration.Nicknames[guid] = _newNickName;
                    _configuration.Save();
                    ImGui.CloseCurrentPopup();
                    Notify.Success("Nickname saved successfully!");
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopup($"##star_note_{guid}"))
            {
                ImGui.Text("Note:");
                ImGui.InputTextMultiline("##note", ref _newNote, 1024, new Vector2(200, 100));
                ImGui.Spacing();
                if (ImGui.Button("Save"))
                {
                    _configuration.Notes[guid] = _newNote;
                    _configuration.Save();
                    ImGui.CloseCurrentPopup();
                    Notify.Success("Note saved successfully!");
                }
                ImGui.EndPopup();
            }
        }

        if (cidToRemove != null)
        {
            _configuration.IndividualPairs.Remove(cidToRemove.Value);
            _configuration.Save();
            Notify.Success("Unpaired successfully!");
        }
    }

    private void PairNewCode()
    {
        var text = ImGui.GetClipboardText();
        if (Guid.TryParse(text, out var guid))
        {
            _configuration.IndividualPairs.Add(guid);
            _configuration.Save();
            Notify.Success("Pair added successfully!");
        }
        else
        {
            Notify.Error("Invalid pairing code!");
        }
    }

    private void DrawCenteredText(string text)
    {
        var textSize = ImGui.CalcTextSize(text);
        var windowWidth = ImGui.GetContentRegionAvail().X;
        var offset = (windowWidth - textSize.X) / 2f;

        if (offset > 0)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        ImGui.Text(text);
    }

    private void DrawCenteredScaledText(string text, float scale)
    {
        ImGui.PushFont(ImGui.GetFont());
        ImGui.SetWindowFontScale(scale);

        var textSize = ImGui.CalcTextSize(text);
        var windowWidth = ImGui.GetContentRegionAvail().X;
        var offset = (windowWidth - textSize.X) / 2f;

        if (offset > 0)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        ImGui.Text(text);

        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopFont();
    }

    private bool DrawCenteredSelectable(string text, ImGuiSelectableFlags flags)
    {
        var windowWidth = ImGui.GetContentRegionAvail().X;
        var clicked = ImGui.Selectable($"##{text}", false, flags, new Vector2(windowWidth, 0));

        var textSize = ImGui.CalcTextSize(text);
        var offset = (windowWidth - textSize.X) / 2f;

        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();
        var textPos = new Vector2(itemMin.X + offset, itemMin.Y);

        ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), text);

        return clicked;
    }

    private void DrawCenteredButton(string text, float width, Action onClick)
    {
        var windowWidth = ImGui.GetContentRegionAvail().X;
        var offset = (windowWidth - width) / 2f;

        if (offset > 0)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);

        if (ImGui.Button(text, new Vector2(width, 0)))
            onClick();
    }
}