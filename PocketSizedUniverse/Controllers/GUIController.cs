using System;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.Commands;
using ECommons.DalamudServices;
using Microsoft.Extensions.Logging;
using PocketSizedUniverse.GUI;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse.Controllers;

public class GUIController : IDisposable
{
    private readonly IUiBuilder _uiBuilder;
    private readonly IpfsService _ipfsService;
    private readonly Config.Configuration _configuration;
    private readonly ILogger<GUIController> _logger;
    private readonly MainWindow _mainWindow;
    private readonly SetupWindow _setupWindow;
    private readonly ConfigWindow _configWindow;
    private readonly CreateEditGalaxyWindow _createEditGalaxyWindow;
    private readonly WindowSystem _windowSystem;

    public GUIController(IUiBuilder uiBuilder, Config.Configuration configuration, ILogger<GUIController> logger,
        MainWindow mainWindow, WindowSystem windowSystem, SetupWindow setupWindow, IpfsService ipfsService, ConfigWindow configWindow, CreateEditGalaxyWindow createEditGalaxyWindow)
    {
        _ipfsService = ipfsService;
        _uiBuilder = uiBuilder;
        _configuration = configuration;
        _logger = logger;
        _mainWindow = mainWindow;
        _setupWindow = setupWindow;
        _configWindow = configWindow;
        _createEditGalaxyWindow = createEditGalaxyWindow;
        _windowSystem = windowSystem;
        _windowSystem.AddWindow(_setupWindow);
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_configWindow);
        _windowSystem.AddWindow(_createEditGalaxyWindow);
        _uiBuilder.Draw += _windowSystem.Draw;
        _uiBuilder.OpenMainUi += _mainWindow.Toggle;
        _uiBuilder.OpenConfigUi += _configWindow.Toggle;
        _logger.LogInformation("GUI Controller Initialized");
        EzCmd.Add("/psu", OnCommand, "Does many things. Use /psu help for more info.");
        if (!IsSetupComplete())
            _setupWindow.IsOpen = true;
    }
    
    private void OnCommand(string command, string args)
    {
        if (string.Equals(args, "help", StringComparison.OrdinalIgnoreCase))
        {
            Svc.Chat.Print("/psu - Opens the main window");
            Svc.Chat.Print("/psu help - Shows this message");
            Svc.Chat.Print("/psu webui - Opens the IPFS web UI");
            Svc.Chat.Print("/psu config - Opens the configuration window");
            return;
        }

        if (string.Equals(args, "webui", StringComparison.OrdinalIgnoreCase))
        {
            _ipfsService.OpenWebUi();
            return;
        }

        if (string.Equals(args, "config", StringComparison.OrdinalIgnoreCase))
        {
            _configWindow.Toggle();
            return;
        }
        if (!IsSetupComplete())
            _setupWindow.IsOpen = true;
        else
            _mainWindow.Toggle();
    }
    
    private bool IsSetupComplete() => !string.IsNullOrEmpty(_configuration.CacheDirectory) && _configuration.IpfsMode != null;
    
    public void Dispose()
    {
        _uiBuilder.Draw -= _windowSystem.Draw;
        _uiBuilder.OpenMainUi -= _mainWindow.Toggle;
    }
}