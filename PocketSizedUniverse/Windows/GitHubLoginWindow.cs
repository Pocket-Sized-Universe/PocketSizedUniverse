using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ECommons.Configuration;
using ECommons.DalamudServices;
using Octokit;

namespace PocketSizedUniverse.Windows;

public class GitHubLoginWindow : Window
{
    public GitHubLoginWindow() : base("Pocket Sized Universe: GitHub Login")
    {
        Size = new Vector2(400, 250);
        Flags |= ImGuiWindowFlags.NoResize;
        SizeCondition = ImGuiCond.Always;
    }

    private Task<OauthDeviceFlowResponse?>? _deviceCodeTask;
    private Task<string?>? _tokenTask;

    public override void Draw()
    {
        ImGui.TextWrapped("GitHub authentication is required for Pocket Sized Universe to be able to manage your Galaxies for you. It is not required if you want to manage your Galaxies manually.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        if (_tokenTask != null && _tokenTask.IsCompletedSuccessfully)
        {
            var token = _tokenTask.Result;
            if (token == null)
            {
                ImGui.Text("Failed to get access token.");
                ImGui.Spacing();
                if (ImGui.Button("Try Again"))
                {
                    _deviceCodeTask = null;
                    _tokenTask = null;
                }
            }
            else
            {
                ImGui.Text("Successfully authenticated with GitHub!");
                ImGui.Spacing();
                ImGui.Text("You may now close this window.");
            }
        }
        else if (_deviceCodeTask == null || _deviceCodeTask.IsFaulted)
        {
            ImGui.TextWrapped("The below button will generate an 8 digit code that you will need to enter on the GitHub login page.");
            ImGui.TextWrapped("PSU will request access to manage public repositories on your behalf. It will only manage repositories related to Galaxies that you create or manage.");
            if (ImGui.Button("Authenticate with GitHub"))
            {
                _deviceCodeTask = Task.Run(PsuPlugin.GitHubService.StartDeviceOAuthFlow);
            }
        }
        else if (!_deviceCodeTask.IsCompletedSuccessfully)
        {
            ImGui.Text("Requesting device code...");
        }
        else if (_deviceCodeTask.IsCompletedSuccessfully)
        {
            var code = _deviceCodeTask.Result;
            if (code == null)
            {
                ImGui.Text("Failed to start device code flow.");
                ImGui.Spacing();
                if (ImGui.Button("Try Again"))
                {
                    _deviceCodeTask = null;
                    _tokenTask = null;
                }
            }
            else
            {
                // Start token task if not already started
                _tokenTask ??= Task.Run(() => PsuPlugin.GitHubService.WaitForAccessToken(code));

                ImGui.Text($"Your Device Code: {code.UserCode}");
                if (ImGui.Button("Copy to Clipboard"))
                {
                    ImGui.SetClipboardText(code.UserCode);
                }
                
                ImGui.TextWrapped("Enter this code on the GitHub login page below and then Authorize PSU to access your GitHub account.");

                ImGui.Text($"Login URL: {code.VerificationUri}");
                if (ImGui.Button("Open in Web Browser"))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = code.VerificationUri,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    catch (Exception ex)
                    {
                        Svc.Log.Error($"Failed to open GitHub link: {ex}");
                    }
                }

                ImGui.Spacing();
                ImGui.Text("Waiting for authentication...");
            }
        }
    }
}