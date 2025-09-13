using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using PocketSizedUniverse.Models.Data;
using OtterGui;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private void DrawMyStarCode()
    {
        var myStarPack = PsuPlugin.Configuration.MyStarPack;
        if (myStarPack == null)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), "No Star Code available. Please complete setup first.");
            return;
        }

        var myStar = myStarPack.GetStar();
        var myDataPack = myStarPack.GetDataPack();

        // Star Code export section
        ImGui.Text("Your Star Code:");
        var starCode = Base64Util.ToBase64(myStarPack);
        SetInputWidth((int)ImGui.GetContentRegionAvail().X);
        ImGui.InputText("##starcode", ref starCode, starCode.Length, ImGuiInputTextFlags.ReadOnly);

        if (ImGui.Button("Copy to Clipboard"))
        {
            ImGui.SetClipboardText(starCode);
            Notify.Info("Copied Star Code to clipboard.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Editors section
        if (ImGui.CollapsingHeader("Star Settings"))
        {
            DrawStarEditor(myStar);
        }

        ImGui.Spacing();
        
        if (ImGui.CollapsingHeader("DataPack Settings"))
        {
            DrawDataPackEditor(myDataPack);
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Save button
        if (_starEditorChanged || _dataPackEditorChanged)
        {
            if (ImGui.Button("Save Changes", new Vector2(120, 0)))
            {
                SaveChanges();
                Notify.Info("Changes saved successfully!");
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Discard Changes", new Vector2(120, 0)))
            {
                // Refresh from service to discard local changes
                PsuPlugin.SyncThingService.InvalidateCaches();
                _starEditorChanged = false;
                _dataPackEditorChanged = false;
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("No Changes", new Vector2(120, 0));
            ImGui.EndDisabled();
        }
    }
}
