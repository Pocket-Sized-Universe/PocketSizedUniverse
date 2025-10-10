using System.Numerics;
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
        if (ImGui.CollapsingHeader("Certificate Manager", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawCertificateManager();
        }

        if (ImGui.CollapsingHeader("Virus Scanner"))
        {
            DrawVirusScanner();
        }
    }

    private void DrawCertificateManager()
    {
        ImGui.Text("Your PSU Certificate");
        ImGui.Separator();
        ImGui.Spacing();

        var cert = PsuPlugin.Configuration.MyCertificate;
        if (cert == null)
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, "No certificate available. Please complete setup first.");
            if (ImGui.Button("Create Certificate"))
            {
                GenCert();
            }
            return;
        }
        ImGui.Text($"Star ID: {cert.StarId}");
        ImGui.Text($"Created: {cert.Created:yy-MM-dd}");
        ImGui.Text($"Expires: {cert.Expiry:yy-MM-dd}");
        if (cert.IsExpired())
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, "Certificate expired! You will not be able to connect to other stars.");
            if (ImGui.Button("Renew Certificate"))
            {
                GenCert();
            }
        }
        
        ImGui.Spacing();
        ImGui.Text("Certificate Fingerprint:");
        ImGui.TextWrapped(cert.GetFingerprint());
        if (ImGui.Button("Copy Fingerprint"))
        {
            ImGui.SetClipboardText(cert.GetFingerprint());
            Notify.Info("Copied certificate fingerprint to clipboard.");
        }
        ImGui.SameLine();
        if (ImGui.Button("Export Public Certificate"))
        {
            ImGui.SetClipboardText(cert.PublicKey);
            Notify.Info("Copied public certificate to clipboard.");
        }
        
        ImGui.Spacing();
        ImGui.Text("Your fingerprint can be shared outside of PSU to verify your identity if necessary.");
        //DrawCertDebug();
    }

    private string _encoderTest = "Honk and/or tonk!";
    private string _signatureTest = string.Empty;
    private void DrawCertDebug()
    {
        ImGui.InputText("Test String", ref _encoderTest, 2048);
        if (ImGui.Button("Encode"))
        {
            var encoded = PsuPlugin.Configuration.MyCertificate!.SignData(_encoderTest);
            _signatureTest = encoded;
        }
        ImGui.SameLine();
        if (ImGui.Button("Decode"))
        {
            var decoded = PsuCertificate.VerifySignature(PsuPlugin.Configuration.MyCertificate!.PublicKey, _encoderTest, _signatureTest);
            if (decoded)
                Notify.Info("Signature verified.");
            else
                Notify.Error("Signature failed to verify.");
        }
    }

    private void GenCert()
    {
        var myStarId = PsuPlugin.Configuration.MyStarPack?.StarId;
        if (myStarId == null)
        {
            Svc.Log.Error("Star not configured, cannot create certificate.");
            return;
        }
        PsuPlugin.Configuration.MyCertificate = PsuCertificate.Generate(myStarId);
        EzConfig.Save();
    }

    private void DrawVirusScanner()
    {
        var enabled = PsuPlugin.Configuration.EnableVirusScanning;
        if (ImGui.Checkbox("Enable Virus Scanning", ref enabled))
        {
            PsuPlugin.Configuration.EnableVirusScanning = enabled;
            EzConfig.Save();
        }
        ImGuiUtil.HoverTooltip("Enables virus scanning of all mod files. Can cause performance issues on lower end systems.");
        ImGui.Spacing();
        ImGui.Separator();

        var results = PsuPlugin.Database.ScanResults;
        var count = results.Count;
        var clean = results.Count(kvp => kvp.Value.Result == ScanResult.ResultType.Clean);
        var infected = results.Count(kvp => kvp.Value.Result == ScanResult.ResultType.Infected);
        ImGui.Text($"Scanned {count} files. {clean} clean, {infected} infected.");
        var pendingFiles = PsuPlugin.AntiVirusScanner.PathsToScan.Count;
        ImGui.Text($"Pending: {pendingFiles}");
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