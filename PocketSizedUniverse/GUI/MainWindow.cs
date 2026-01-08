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
    private readonly CreateEditGalaxyWindow _createEditGalaxyWindow;
    private readonly ModController _modController;
    private readonly DataController _dataController;
    private readonly Config.Configuration _configuration;
    private readonly IpfsService _ipfsService;

    private const float FixedWindowWidth = 400f;
    private const int TruncatedCodeLength = 12;
    private const float HeaderFontScale = 1.3f;
    private const float TabFontScale = 1.15f;

    public MainWindow(ModController modController, DataController dataController, Config.Configuration configuration,
        IpfsService ipfsService, CreateEditGalaxyWindow createEditGalaxyWindow) : base(
        "Pocket Sized Universe", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _configuration = configuration;
        _modController = modController;
        _dataController = dataController;
        _ipfsService = ipfsService;
        _createEditGalaxyWindow = createEditGalaxyWindow;

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
        DrawCenteredText("Coming soon...");
    }

    private void DrawGalaxies()
    {
        DrawGalaxyTabSelector();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();

        switch (_galaxyDrawState)
        {
            case GalaxyDrawState.Subscribed:
                DrawSubscribedGalaxies();
                break;
            case GalaxyDrawState.Managed:
                DrawManagedGalaxies();
                break;
        }
    }

    private void DrawManagedGalaxies()
    {
        var buttonWidth = 200f;
        ImGui.Spacing();
        DrawCenteredButton("Create New Galaxy", buttonWidth, CreateNewGalaxy);
        var controlledGalaxies = _configuration.ControlledGalaxies;
        if (controlledGalaxies.Count > 0)
        {
            var cidToRemove = (KeyValuePair<Cid, GalaxyData>?)null;

            foreach (var galaxy in controlledGalaxies)
            {
                var fullCode = galaxy.Key.ToString();
                var truncatedCode = fullCode;

                var windowWidth = ImGui.GetContentRegionAvail().X;
                var clicked = ImGui.Selectable($"##star_{fullCode}", false, ImGuiSelectableFlags.None,
                    new Vector2(windowWidth, 0));

                var textSize = ImGui.CalcTextSize(truncatedCode);
                var offset = (windowWidth - textSize.X) / 2f;
                var itemMin = ImGui.GetItemRectMin();
                var textPos = new Vector2(itemMin.X + offset, itemMin.Y);

                ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), truncatedCode);

                if (clicked)
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

                if (ImGui.BeginPopupContextItem($"##galaxy_context_{fullCode}"))
                {
                    if (ImGui.MenuItem("Copy Galaxy Code"))
                    {
                        ImGui.SetClipboardText(fullCode);
                        Notify.Success("Galaxy code copied to clipboard!");
                    }

                    if (ImGui.MenuItem("Edit Galaxy"))
                    {
                        _createEditGalaxyWindow.OpenForGalaxy(galaxy.Key, galaxy.Value);
                    }

                    if (ImGui.MenuItem("Remove Galaxy"))
                    {
                        cidToRemove = galaxy;
                    }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Add Nickname"))
                    {
                        Notify.Info("Coming soon!");
                    }

                    if (ImGui.MenuItem("Add Note"))
                    {
                        Notify.Info("Coming soon!");
                    }

                    ImGui.EndPopup();
                }
            }

            if (cidToRemove != null)
            {
                _configuration.ControlledGalaxies.Remove(cidToRemove.Value.Key, out _);
                _configuration.Save();
                Notify.Success("Unpaired successfully!");
            }
        }
        else
        {
            ImGui.Spacing();
            DrawCenteredText("You're not managing any Galaxies.");
        }
    }

    private void DrawSubscribedGalaxies()
    {
        ImGui.Spacing();
        var buttonWidth = 200f;
        if (_subscribeGalaxyTask != null && !_subscribeGalaxyTask.IsCompleted)
        {
            ImGui.BeginDisabled();
            DrawCenteredButton("Subscribing...", buttonWidth, SubscribeToGalaxy);
            ImGui.EndDisabled();
        }
        else
        {
            DrawCenteredButton("Subscribe to Galaxy", buttonWidth, SubscribeToGalaxy);
        }

        var subscribedGalaxies = _configuration.SubscribedGalaxies;
        if (subscribedGalaxies.Count > 0)
        {
            var cidToRemove = (Cid?)null;

            foreach (var cid in subscribedGalaxies)
            {
                var fullCode = cid.ToString();
                var truncatedCode = fullCode;

                var windowWidth = ImGui.GetContentRegionAvail().X;
                var clicked = ImGui.Selectable($"##star_{fullCode}", false, ImGuiSelectableFlags.None,
                    new Vector2(windowWidth, 0));

                var textSize = ImGui.CalcTextSize(truncatedCode);
                var offset = (windowWidth - textSize.X) / 2f;
                var itemMin = ImGui.GetItemRectMin();
                var textPos = new Vector2(itemMin.X + offset, itemMin.Y);

                ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), truncatedCode);

                if (clicked)
                {
                    ImGui.SetClipboardText(fullCode);
                    Notify.Success("Galaxy code copied to clipboard!");
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(fullCode);
                    ImGui.EndTooltip();
                }

                if (ImGui.BeginPopupContextItem($"##galaxy_context_{fullCode}"))
                {
                    if (ImGui.MenuItem("Copy Galaxy Code"))
                    {
                        ImGui.SetClipboardText(fullCode);
                        Notify.Success("Pairing code copied to clipboard!");
                    }

                    if (ImGui.MenuItem("Unsubscribe"))
                    {
                        cidToRemove = cid;
                    }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Add Nickname"))
                    {
                        Notify.Info("Coming soon!");
                    }

                    if (ImGui.MenuItem("Add Note"))
                    {
                        Notify.Info("Coming soon!");
                    }

                    ImGui.EndPopup();
                }
            }

            if (cidToRemove != null)
            {
                _configuration.SubscribedGalaxies.Remove(cidToRemove);
                _configuration.Save();
                Notify.Success("Unsubscribed successfully!");
            }
        }
        else
        {
            ImGui.Spacing();
            DrawCenteredText("You're not subscribed to any Galaxies.");
        }
    }

    private void CreateNewGalaxy()
    {
        var galaxyData = new GalaxyData();
        _createEditGalaxyWindow.OpenForGalaxy(null, galaxyData);
    }

    private Task? _subscribeGalaxyTask;

    private void SubscribeToGalaxy()
    {
        var text = ImGui.GetClipboardText();
        _subscribeGalaxyTask = Task.Run(async () =>
        {
            try
            {
                if (string.IsNullOrEmpty(text) || text.StartsWith("Qm"))
                    throw new FormatException();

                var cidToPair = Cid.Decode(text);

                if (_configuration.SubscribedGalaxies.Contains(cidToPair))
                {
                    Notify.Error("Already subscribed!");
                    return;
                }

                if (_configuration.ControlledGalaxies.ContainsKey(cidToPair))
                {
                    Notify.Error("Cannot subscribe to your own Galaxy!");
                    return;
                }

                var dataCid = await _ipfsService.ResolveName(cidToPair.ToString());
                if (dataCid == null)
                {
                    Notify.Error("Galaxy not found!");
                    return;
                }

                var galaxyDataPath = await _ipfsService.ResolveCidToFilePath(dataCid);
                if (galaxyDataPath == null)
                {
                    Notify.Error("Galaxy not found!");
                    return;
                }

                var galaxyDataBase = await File.ReadAllTextAsync(galaxyDataPath);
                var galaxyData = Base64Util.FromBase64<GalaxyData>(galaxyDataBase);
                if (galaxyData == null)
                {
                    Notify.Error("Galaxy data was not valid.");
                    return;
                }

                _configuration.SubscribedGalaxies.Add(cidToPair);
                _configuration.Save();
                Notify.Success("Successfully subscribed to Galaxy!");
            }
            catch (FormatException)
            {
                Notify.Error("Invalid Galaxy Code!");
            }
        });
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
            var cidToRemove = (Guid?)null;

            foreach (var cid in pairs)
            {
                var fullCode = cid.ToString();
                var truncatedCode = fullCode;

                var windowWidth = ImGui.GetContentRegionAvail().X;
                var clicked = ImGui.Selectable($"##star_{fullCode}", false, ImGuiSelectableFlags.None,
                    new Vector2(windowWidth, 0));

                var textSize = ImGui.CalcTextSize(truncatedCode);
                var offset = (windowWidth - textSize.X) / 2f;
                var itemMin = ImGui.GetItemRectMin();
                var textPos = new Vector2(itemMin.X + offset, itemMin.Y);

                ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), truncatedCode);

                if (clicked)
                {
                    ImGui.SetClipboardText(fullCode);
                    Notify.Success("Pairing code copied to clipboard!");
                }

                if (ImGui.BeginPopupContextItem($"##star_context_{fullCode}"))
                {
                    if (ImGui.MenuItem("Copy Pairing Code"))
                    {
                        ImGui.SetClipboardText(fullCode);
                        Notify.Success("Pairing code copied to clipboard!");
                    }

                    if (ImGui.MenuItem("Unpair"))
                    {
                        cidToRemove = cid;
                    }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Add Nickname"))
                    {
                        Notify.Info("Coming soon!");
                    }

                    if (ImGui.MenuItem("Add Note"))
                    {
                        Notify.Info("Coming soon!");
                    }

                    if (ImGui.MenuItem("Start Chat"))
                    {
                        Notify.Info("Coming soon!");
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
        else
        {
            ImGui.Spacing();
            DrawCenteredText("You're not paired with anyone.");
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