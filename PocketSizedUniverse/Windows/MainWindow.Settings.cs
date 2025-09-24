using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using ECommons.Configuration;
using ECommons.DalamudServices;
using OtterGui;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Models.Data;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private readonly FileDialogManager _settingsFileDialogManager = new();
    private bool _settingsChanged = false;
    private string _blocklistInput = string.Empty;

    private void DrawSettings()
    {
        _settingsChanged = false;

        DrawSyncThingSettings();
        ImGui.Spacing();

        DrawGeneralSettings();

        DrawTransientDataSettings();

        // Handle file dialogs
        _settingsFileDialogManager.Draw();

        // Save configuration if any changes were made this frame
        if (_settingsChanged)
        {
            EzConfig.Save();
        }
    }

    private void DrawTransientDataSettings()
    {
        if (ImGui.CollapsingHeader("Transient Data", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.Button("Clear Transient Data"))
            {
                PsuPlugin.Configuration.TransientFiles.Clear();
                EzConfig.Save();
            }
            ImGuiUtil.HoverTooltip("Click this button if things like VFX and animations are acting weird.");
        }
    }

    #region SyncThing Settings

    private void DrawSyncThingSettings()
    {
        if (ImGui.CollapsingHeader("SyncThing Configuration", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var config = PsuPlugin.Configuration;

            // Use Easy Mode checkbox
            var useBuiltIn = config.UseBuiltInSyncThing;
            if (ImGui.Checkbox("Use Easy Mode", ref useBuiltIn))
            {
                config.UseBuiltInSyncThing = useBuiltIn;
                _settingsChanged = true;
            }

            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "(Recommended for most users)");

            // Only show advanced settings if not using built-in mode
            if (!config.UseBuiltInSyncThing)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Text("Advanced SyncThing Settings:");
                ImGui.Spacing();

                DrawAdvancedSyncThingSettings();
            }

            if (ImGui.Button("Open SyncThing Interface"))
            {
                try
                {
                    // Use ProcessStartInfo with the URL directly
                    var psi = new ProcessStartInfo
                    {
                        FileName = PsuPlugin.Configuration.ApiUri!.ToString(),
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Failed to open SyncThing interface: {ex}");
                }
            }
        }
    }

    private void DrawAdvancedSyncThingSettings()
    {
        var config = PsuPlugin.Configuration;

        // API Key input
        var apiKey = config.ApiKey ?? "";
        SetInputWidth(300);
        if (ImGui.InputText("API Key", ref apiKey, 256))
        {
            config.ApiKey = apiKey;
            _settingsChanged = true;
        }

        // API URI input
        var apiUri = config.ApiUri?.ToString() ?? "";
        SetInputWidth(300);
        if (ImGui.InputText("API URI", ref apiUri, 512))
        {
            if (Uri.TryCreate(apiUri, UriKind.Absolute, out var uri))
            {
                config.ApiUri = uri;
                _settingsChanged = true;
            }
            else if (string.IsNullOrWhiteSpace(apiUri))
            {
                config.ApiUri = null;
                _settingsChanged = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(apiUri) && config.ApiUri == null)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), "Invalid URI format");
        }
    }

    #endregion

    #region General Settings

    private void DrawGeneralSettings()
    {
        if (ImGui.CollapsingHeader("General Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawDataPackDirectorySettings();
            ImGui.Spacing();
            DrawBlocklistSettings();
        }
    }

    private void DrawDataPackDirectorySettings()
    {
        var config = PsuPlugin.Configuration;

        DrawFolderSelector(
            "Default Data Pack Directory",
            config.DefaultDataPackDirectory,
            "Select Default Data Pack Directory",
            OnDataPackDirectorySelected
        );

        if (!string.IsNullOrWhiteSpace(config.DefaultDataPackDirectory))
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f),
                $"DataPacks will be created in: {config.DefaultDataPackDirectory}");
        }
    }

    private void OnDataPackDirectorySelected(bool success, string path)
    {
        if (success && !string.IsNullOrWhiteSpace(path))
        {
            PsuPlugin.Configuration.DefaultDataPackDirectory = path;
            EzConfig.Save(); // Immediate save for file dialog results
        }
    }

    #endregion

    private void DrawBlocklistSettings()
    {
        var config = PsuPlugin.Configuration;
        if (ImGui.CollapsingHeader("Blocklist", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextWrapped("Star Codes you add here will be ignored by automatic healing and linking.");
            ImGui.Spacing();

            // Input
            SetInputWidth(400);
            ImGui.InputText("Blocklist Star Code", ref _blocklistInput, 2048);
            ImGui.SameLine();
            if (ImGui.Button("Paste from Clipboard"))
            {
                var clip = ImGui.GetClipboardText();
                if (!string.IsNullOrWhiteSpace(clip))
                    _blocklistInput = clip.Trim();
            }

            ImGui.SameLine();
            if (ImGui.Button("Add"))
            {
                try
                {
                    var sp = Base64Util.FromBase64<StarPack>(_blocklistInput);
                    if (sp != null)
                    {
                        var exists = config.Blocklist.Any(b => b.StarId == sp.StarId && b.DataPackId == sp.DataPackId);
                        if (!exists)
                        {
                            config.Blocklist.Add(sp);
                            _settingsChanged = true;
                            _blocklistInput = string.Empty;
                        }
                    }
                }
                catch
                {
                    // ignore invalid input for now
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // List existing blocklist entries
            if (config.Blocklist.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "No blocked Star Codes.");
            }
            else
            {
                for (int i = 0; i < config.Blocklist.Count; i++)
                {
                    var sp = config.Blocklist[i];
                    var star = sp.GetStar();
                    var dataPack = sp.GetDataPack();
                    var starName = star?.Name ?? $"Star-{(sp.StarId.Length >= 8 ? sp.StarId[..8] : sp.StarId)}";
                    var packName = dataPack?.Name ?? sp.DataPackId.ToString();

                    ImGui.Text($"{starName}  →  {packName}");
                    ImGui.SameLine();
                    if (ImGui.Button($"Unblock##{i}"))
                    {
                        config.Blocklist.RemoveAt(i);
                        _settingsChanged = true;
                        i--;
                    }
                }
            }
        }
    }

    #region Future Settings

    // Placeholder for future settings sections:
    //
    // private void DrawNetworkingSettings() { }
    // private void DrawUISettings() { }
    // private void DrawAdvancedSettings() { }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Draws a folder selector with input field and browse button
    /// </summary>
    private void DrawFolderSelector(string label, string currentPath, string dialogTitle,
        Action<bool, string> onSelected)
    {
        ImGui.Text($"{label}:");

        var displayPath = currentPath ?? "";
        SetInputWidth(400);
        ImGui.InputText($"##{label.Replace(" ", "")}", ref displayPath, 512, ImGuiInputTextFlags.ReadOnly);

        ImGui.SameLine();
        if (ImGui.Button("Browse...", new Vector2(80, 0)))
        {
            _settingsFileDialogManager.OpenFolderDialog(dialogTitle, onSelected, currentPath ?? "", false);
        }

        if (!string.IsNullOrWhiteSpace(displayPath))
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f),
                $"Path: {displayPath}");
        }
    }

    #endregion
}