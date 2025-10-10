using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using ECommons.Configuration;
using ECommons.DalamudServices;
using PocketSizedUniverse.Models;
using PocketSizedUniverse.Windows.ViewModels;
using Syncthing.Models.Response;
using OtterGui;
using System.Threading.Tasks;
using OtterGui.Text;

namespace PocketSizedUniverse.Windows;

public class SetupWindow : Window
{
    // Temporary UI state (not persisted)
    private string _tempServerUri = "http://127.0.0.1:" + Random.Shared.Next(10000, 60000);
    private string _tempApiKey = "";
    private string _tempDataPackName = Adjectives.GetRandom() + " " + Nouns.GetRandom();
    private readonly FileDialogManager _fileDialogManager = new();

    // Connection monitoring
    private DateTime _connectionStartTime;
    private bool _connectionAttempted = false;

    // Star editing state
    private Star? _editingStar;
    private string _tempStarName = "";
    private int _tempMaxSendKbps = 0;
    private int _tempMaxRecvKbps = 0;
    private bool _tempPaused = false;
    private string _tempCompression = "metadata";

    public SetupWindow() : base("Pocket Sized Universe - First Time Setup")
    {
        Size = new System.Numerics.Vector2(700, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags |= ImGuiWindowFlags.NoResize;
    }

    public override bool DrawConditions() => !PsuPlugin.Configuration.SetupComplete;

    public override void Draw()
    {
        // Determine current step based on configuration state and SyncThing health
        if (PsuPlugin.Configuration.ApiUri == null || !PsuPlugin.SyncThingService.IsHealthy)
        {
            DrawProgressIndicator(1, 3, "Connect to SyncThing");
            DrawConnectionStep();
        }
        else if (!HasConfiguredStar())
        {
            DrawProgressIndicator(2, 3, "Configure Your Star");
            DrawStarConfigurationStep();
        }
        else if (PsuPlugin.Configuration.MyStarPack == null)
        {
            DrawProgressIndicator(3, 3, "Create DataPack");
            DrawDataPackCreationStep();
        }
        else
        {
            DrawWelcomeStep();
        }

        _fileDialogManager.Draw();
    }

    private bool HasConfiguredStar()
    {
        // Check if we have stars available AND the user has completed the configuration
        return !PsuPlugin.SyncThingService.Stars.IsEmpty && PsuPlugin.Configuration.StarConfigurationComplete;
    }

    private void DrawProgressIndicator(int currentStep, int totalSteps, string stepName)
    {
        ImGui.Text("Setup Progress:");
        ImGui.SameLine();

        for (int i = 1; i <= totalSteps; i++)
        {
            if (i > 1) ImGui.SameLine();

            var color = i <= currentStep
                ? ImGui.GetColorU32(ImGuiCol.Text)
                : ImGui.GetColorU32(ImGuiCol.TextDisabled);

            ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(color),
                i <= currentStep ? "●" : "○");

            if (i < totalSteps)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(color), "─");
            }
        }

        ImGui.Spacing();
        ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TextDisabled)),
            $"Step {currentStep} of {totalSteps}: {stepName}");
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawConnectionStep()
    {
        // Header with icon
        ImGuiUtil.PrintIcon(FontAwesomeIcon.Plug);
        ImGui.SameLine();
        ImGui.Text(" Connect to SyncThing Engine");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped(
            "Welcome to Pocket Sized Universe! Let's connect to the SyncThing engine that powers file synchronization.");
        ImGui.Spacing();

        // Show connection status if we have configuration but no health
        if (PsuPlugin.Configuration.ApiUri != null && !PsuPlugin.SyncThingService.IsHealthy && _connectionAttempted)
        {
            var elapsed = (DateTime.UtcNow - _connectionStartTime).TotalSeconds;

            ImGuiUtil.PrintIcon(FontAwesomeIcon.ExclamationTriangle);
            ImGui.SameLine();
            ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.Button)),
                $"Connection failed or timed out ({elapsed:F1}s). Please check your settings.");

            ImGui.Spacing();
            if (ImGui.Button("Reset Connection", new System.Numerics.Vector2(150, 0)))
            {
                ResetConnection();
            }

            return;
        }

        // Show connection success
        if (PsuPlugin.SyncThingService.IsHealthy)
        {
            ImGuiUtil.PrintIcon(FontAwesomeIcon.CheckCircle);
            ImGui.SameLine();
            ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.Text)),
                "Successfully connected to SyncThing!");
            ImGui.Spacing();
            ImGui.Text("The setup will continue automatically...");
            return; // Connection successful, UI will advance automatically
        }

        var useBuiltIn = PsuPlugin.Configuration.UseBuiltInSyncThing;

        if (ImGui.RadioButton("Easy Setup (Recommended)", useBuiltIn))
        {
            useBuiltIn = true;
            PsuPlugin.Configuration.UseBuiltInSyncThing = useBuiltIn;
            EzConfig.Save();
        }

        ImGui.Indent();
        ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TextDisabled)),
            "Uses the built-in SyncThing server. Perfect for most users.");
        ImGui.Unindent();

        if (ImGui.RadioButton("Advanced Setup", !useBuiltIn))
        {
            useBuiltIn = false;
            PsuPlugin.Configuration.UseBuiltInSyncThing = useBuiltIn;
            EzConfig.Save();
        }

        ImGui.Indent();
        ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TextDisabled)),
            "Connect to your own SyncThing server.");
        ImGui.Unindent();

        if (!useBuiltIn)
        {
            ImGui.Spacing();
            ImGui.InputText("Server URI", ref _tempServerUri, 256);
            ImGui.InputText("API Key", ref _tempApiKey, 256);
        }

        ImGui.Spacing();
        ImGui.Separator();

        var canProceed = useBuiltIn ||
                         (!string.IsNullOrWhiteSpace(_tempServerUri) && !string.IsNullOrWhiteSpace(_tempApiKey));

        if (!canProceed)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Connect", new System.Numerics.Vector2(100, 0)))
        {
            StartConnection(useBuiltIn);
        }

        if (!canProceed)
        {
            ImGui.EndDisabled();
        }
    }

    private void StartConnection(bool useBuiltIn)
    {
        _connectionAttempted = true;
        _connectionStartTime = DateTime.UtcNow;

        // Configure connection based on choice
        if (useBuiltIn)
        {
            PsuPlugin.Configuration.UseBuiltInSyncThing = true;
            PsuPlugin.Configuration.ApiKey = Guid.NewGuid().ToString().Replace("-", "");
            PsuPlugin.Configuration.ApiUri = new Uri(_tempServerUri);

            // Start built-in server
            try
            {
                PsuPlugin.StartServer();
                Svc.Log.Information("Started built-in SyncThing server");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Failed to start built-in server: {ex}");
                return;
            }
        }

        else
        {
            PsuPlugin.Configuration.UseBuiltInSyncThing = false;
            PsuPlugin.Configuration.ApiKey = _tempApiKey;
            PsuPlugin.Configuration.ApiUri = new Uri(_tempServerUri);
        }

        // Save configuration and reinitialize service
        EzConfig.Save();
        PsuPlugin.SyncThingService?.InitializeClient();
    }

    private void ResetConnection()
    {
        _connectionAttempted = false;
        PsuPlugin.Configuration.ApiUri = null;
        PsuPlugin.Configuration.ApiKey = null;
        PsuPlugin.Configuration.UseBuiltInSyncThing = false;
        PsuPlugin.Configuration.StarConfigurationComplete = false;
        EzConfig.Save();
    }

    private void DrawStarConfigurationStep()
    {
        // Header with icon
        ImGuiUtil.PrintIcon(FontAwesomeIcon.Star);
        ImGui.SameLine();
        ImGui.Text(" Configure Your Star");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped(
            "Great! Now let's configure your Star. This is how other users will see your device in the network.");
        ImGui.Spacing();

        // Get the first star from SyncThingService
        var firstStar = PsuPlugin.SyncThingService.Stars.Values.FirstOrDefault();
        if (firstStar == null)
        {
            ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.Button)),
                "No stars found in SyncThing. Please wait for the service to refresh.");
            return;
        }

        // Initialize editing state if not already done
        if (_editingStar == null || _editingStar.StarId != firstStar.StarId)
        {
            _editingStar = firstStar;
            _tempStarName = Adjectives.GetRandom() + " " + Nouns.GetRandom();
            _tempMaxSendKbps = firstStar.MaxSendKbps;
            _tempMaxRecvKbps = firstStar.MaxRecvKbps;
            _tempPaused = firstStar.Paused;
            _tempCompression = firstStar.Compression ?? "metadata";
        }

        ImGui.Text($"Star ID: {firstStar.StarId}");
        ImGui.Spacing();

        ImGui.InputText("Star Name", ref _tempStarName, 128);
        ImGui.SameLine();
        if (ImUtf8.IconButton(FontAwesomeIcon.Recycle))
        {
            _tempStarName = Adjectives.GetRandom() + " " + Nouns.GetRandom();
        }

        ImGui.Spacing();
        ImGui.Text("Bandwidth Limits (0 = unlimited):");
        ImGui.SliderInt("Max Upload (KB/s)", ref _tempMaxSendKbps, 0, 10000);
        ImGui.SliderInt("Max Download (KB/s)", ref _tempMaxRecvKbps, 0, 10000);

        ImGui.Spacing();
        ImGui.Checkbox("Pause synchronization", ref _tempPaused);

        var compressionTypes = new[] { "always", "metadata", "never" };
        var currentCompressionIndex = Array.IndexOf(compressionTypes, _tempCompression);
        if (currentCompressionIndex == -1) currentCompressionIndex = 1; // default to metadata

        if (ImGui.Combo("Compression", ref currentCompressionIndex, compressionTypes, compressionTypes.Length))
        {
            _tempCompression = compressionTypes[currentCompressionIndex];
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Navigation
        if (ImGui.Button("< Back", new System.Numerics.Vector2(80, 0)))
        {
            ResetConnection();
        }

        ImGui.SameLine();

        var canProceed = !string.IsNullOrWhiteSpace(_tempStarName);
        if (!canProceed)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Save & Continue", new System.Numerics.Vector2(140, 0)))
        {
            SaveStarConfiguration();
        }

        if (!canProceed)
        {
            ImGui.EndDisabled();
        }
    }

    private void SaveStarConfiguration()
    {
        if (_editingStar == null) return;

        // Update the star object
        _editingStar.Name = _tempStarName;
        _editingStar.MaxSendKbps = _tempMaxSendKbps;
        _editingStar.MaxRecvKbps = _tempMaxRecvKbps;
        _editingStar.Paused = _tempPaused;
        _editingStar.Compression = _tempCompression;

        // Post the updated star back to the SyncThing API
        _ = Task.Run(async () =>
        {
            try
            {
                await PsuPlugin.SyncThingService.PostNewStar(_editingStar);
                Svc.Log.Information($"Successfully updated star configuration: {_editingStar.Name}");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Failed to update star configuration: {ex}");
            }
        });

        // Update the cache
        PsuPlugin.SyncThingService.Stars.AddOrUpdate(_editingStar.StarId, _editingStar, (_, _) => _editingStar);

        // Mark star configuration as complete
        PsuPlugin.Configuration.StarConfigurationComplete = true;
        EzConfig.Save();
    }


    private void DrawDataPackCreationStep()
    {
        // Header with icon
        ImGuiUtil.PrintIcon(FontAwesomeIcon.FolderOpen);
        ImGui.SameLine();
        ImGui.Text(" Create Your DataPack");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped(
            "Finally, let's create your personal DataPack. This is where your mods and customization files will be stored for syncing with other Stars.");
        ImGui.Spacing();

        ImGui.InputText("DataPack Name", ref _tempDataPackName, 128);
        ImGui.SameLine();
        if (ImUtf8.IconButton(FontAwesomeIcon.Recycle))
        {
            _tempDataPackName = Adjectives.GetRandom() + " " + Nouns.GetRandom();
        }

        ImGui.Spacing();

        // Set up default directory if not set
        if (PsuPlugin.Configuration.DefaultDataPackDirectory == null)
        {
            var defaultPath = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "DataPacks");
            PsuPlugin.Configuration.DefaultDataPackDirectory = defaultPath;
            EzConfig.Save();
        }

        var dataPackPath = Path.Combine(PsuPlugin.Configuration.DefaultDataPackDirectory, _tempDataPackName);

        ImGui.Text($"Location: {dataPackPath}");

        ImGui.Spacing();
        if (ImGui.Button("Choose Different Location...", new System.Numerics.Vector2(200, 0)))
        {
            _fileDialogManager.OpenFolderDialog("Select DataPack Directory", (success, path) =>
            {
                if (success && !string.IsNullOrEmpty(path))
                {
                    PsuPlugin.Configuration.DefaultDataPackDirectory = path;
                    EzConfig.Save();
                }
            }, PsuPlugin.Configuration.DefaultDataPackDirectory, false);
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Navigation
        if (ImGui.Button("< Back", new System.Numerics.Vector2(80, 0)))
        {
            // Reset star configuration to go back
            _editingStar = null;
            PsuPlugin.Configuration.StarConfigurationComplete = false;
            EzConfig.Save();
        }

        ImGui.SameLine();

        var canProceed = !string.IsNullOrWhiteSpace(_tempDataPackName);
        if (!canProceed)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Create DataPack", new System.Numerics.Vector2(120, 0)))
        {
            CreateDataPack();
        }

        if (!canProceed)
        {
            ImGui.EndDisabled();
        }
    }

    private void CreateDataPack()
    {
        try
        {
            var dataPackId = Guid.NewGuid();
            var dataPackPath = Path.Combine(PsuPlugin.Configuration.DefaultDataPackDirectory!, _tempDataPackName);

            // Get the configured star
            var firstStar = PsuPlugin.SyncThingService.Stars.Values.FirstOrDefault();
            if (firstStar == null)
            {
                Svc.Chat.PrintError("No configured star found. Please restart setup.");
                return;
            }

            // Create the DataPack
            var dataPack = new DataPack(dataPackId)
            {
                Name = _tempDataPackName,
                Path = dataPackPath,
                Type = FolderType.SendOnly,
                IgnorePerms = true,
                AutoNormalize = true,
                RescanIntervalS = 3600,
                FsWatcherEnabled = true,
                FsWatcherDelayS = 10,
                MinDiskFree = new MinDiskFree { Value = 2000, Unit = "MB" },
                Stars = [firstStar]
            };

            // Create directories
            dataPack.EnsureFolders();

            // Post to SyncThing API
            PsuPlugin.SyncThingService.PostNewDataPack(dataPack);

            // Update configuration
            var starPack = new StarPack(firstStar.StarId, dataPackId);
            PsuPlugin.Configuration.MyStarPack = starPack;
            EzConfig.Save();

            // Add to cache
            PsuPlugin.SyncThingService.DataPacks.AddOrUpdate(dataPackId, dataPack, (_, _) => dataPack);

            Svc.Chat.Print($"DataPack '{_tempDataPackName}' created successfully!");
            Svc.Log.Information($"Created DataPack: {dataPack.Name} at {dataPack.Path}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to create DataPack: {ex}");
            Svc.Chat.PrintError($"Failed to create DataPack: {ex.Message}");
        }
    }

    private void DrawWelcomeStep()
    {
        // Header with icon
        ImGuiUtil.PrintIcon(FontAwesomeIcon.CheckCircle);
        ImGui.SameLine();
        ImGui.Text(" Setup Complete!");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped(" Congratulations! Your Pocket Sized Universe is ready to go!");
        ImGui.Spacing();

        ImGui.TextWrapped("Your setup includes:");

        var firstStar = PsuPlugin.SyncThingService.Stars.Values.FirstOrDefault();
        if (firstStar != null)
        {
            ImGuiUtil.PrintIcon(FontAwesomeIcon.Star);
            ImGui.SameLine();
            ImGui.Text($"Star: {firstStar.Name}");
        }

        if (PsuPlugin.Configuration.MyStarPack != null)
        {
            var dataPack =
                PsuPlugin.SyncThingService.DataPacks.Values.FirstOrDefault(dp =>
                    dp.Id == PsuPlugin.Configuration.MyStarPack.DataPackId);
            if (dataPack != null)
            {
                ImGuiUtil.PrintIcon(FontAwesomeIcon.FolderOpen);
                ImGui.SameLine();
                ImGui.Text($"DataPack: {dataPack.Name}");
            }
        }

        ImGui.Spacing();
        ImGui.TextWrapped("You can now:");
        ImGui.BulletText("Share your Star Code with friends to sync mods");
        ImGui.BulletText("Add other Stars to your galaxy");
        ImGui.BulletText("Customize your synchronization settings");

        ImGui.Spacing();
        ImGui.Separator();

        if (ImGui.Button("Finish Setup", new System.Numerics.Vector2(120, 0)))
        {
            CompleteSetup();
        }
    }

    private void CompleteSetup()
    {
        // Mark setup as complete
        PsuPlugin.Configuration.SetupComplete = true;
        EzConfig.Save();

        Svc.Log.Information("Setup completed successfully!");

        IsOpen = false;

        // Open the main window
        if (PsuPlugin.MainWindow != null)
        {
            PsuPlugin.MainWindow.IsOpen = true;
        }
    }
}