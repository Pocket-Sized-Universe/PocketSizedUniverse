using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using OtterGui;
using PocketSizedUniverse.Models;

namespace PocketSizedUniverse.Windows;

public partial class MainWindow
{
    private void DrawSecurity()
    {
        var enabled = PsuPlugin.Configuration.EnableVirusScanning;
        if (ImGui.Checkbox("Enable Virus Scanning", ref enabled))
        {
            PsuPlugin.Configuration.EnableVirusScanning = enabled;
            EzConfig.Save();
        }
        ImGuiUtil.HoverTooltip("Enables virus scanning of all mod files. Can cause performance issues on lower end systems.");
        if (ImGui.Button("Refresh Database"))
        {
            if (PsuPlugin.FreshclamProcess.HasExited)
            {
                PsuPlugin.FreshclamProcess.Start();
                PsuPlugin.FreshclamProcess.BeginOutputReadLine();
                PsuPlugin.FreshclamProcess.BeginErrorReadLine();
            }
            else
                Notify.Error("Database update already in progress");
        }
        ImGuiUtil.HoverTooltip("Force the anti-virus database to update. This is usually unnecessary as the database is updated automatically.");
        ImGui.Spacing();
        ImGui.Separator();

        var results = PsuPlugin.Configuration.ScanResults;
        var count = results.Count;
        var clean = results.Count(kvp => kvp.Value.Result == ScanResult.ResultType.Clean);
        var infected = results.Count(kvp => kvp.Value.Result == ScanResult.ResultType.Infected);
        ImGui.Text($"Scanned {count} files. {clean} clean, {infected} infected.");
        var pendingFiles = PsuPlugin.ClamScanProcess?.PathsToScan.Count;
        ImGui.Text($"Pending: {pendingFiles ?? 0}");
        ImGui.Spacing();
        
        if (ImGui.BeginChild("ScanResultsChild", new System.Numerics.Vector2(0, 0), true))
        {
            ImGui.BeginTable("ScanResults3", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg);
            ImGui.TableSetupColumn("File", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Result", ImGuiTableColumnFlags.WidthFixed, 80f);
            ImGui.TableHeadersRow();
            foreach (var kvp in results)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(kvp.Key);
                ImGuiUtil.HoverTooltip(kvp.Key);
                ImGui.TableNextColumn();
                var resultString = kvp.Value.Result == ScanResult.ResultType.Clean ? "Clean" : kvp.Value.MalwareIdentifier;
                ImGui.TextUnformatted(resultString);
            }
            ImGui.EndTable();
        }
        ImGui.EndChild();
    }
}