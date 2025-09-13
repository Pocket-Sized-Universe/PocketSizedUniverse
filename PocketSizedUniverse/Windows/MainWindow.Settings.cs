using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using ECommons.Configuration;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private readonly FileDialogManager _settingsFileDialogManager = new();
    private bool _settingsChanged = false;

    private void DrawSettings()
    {
        _settingsChanged = false;
        
        DrawSyncThingSettings();
        ImGui.Spacing();
        
        DrawGeneralSettings();
        
        // Future settings sections can be added here:
        // DrawNetworkingSettings();
        // DrawUISettings();
        // DrawAdvancedSettings();
        
        // Handle file dialogs
        _settingsFileDialogManager.Draw();
        
        // Save configuration if any changes were made this frame
        if (_settingsChanged)
        {
            EzConfig.Save();
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
    private void DrawFolderSelector(string label, string currentPath, string dialogTitle, Action<bool, string> onSelected)
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
