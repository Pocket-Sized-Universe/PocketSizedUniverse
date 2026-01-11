using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using HaselCommon.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PocketSizedUniverse.Controllers;
using PocketSizedUniverse.GUI;
using PocketSizedUniverse.Services;

namespace PocketSizedUniverse;

public class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IHost _host;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        ECommonsMain.Init(_pluginInterface, this);
        var builder = new HostBuilder()
            .UseContentRoot(pluginInterface.AssemblyLocation.Directory!.FullName)
            .ConfigureServices(services =>
            {
                services.AddDalamud(_pluginInterface);
                services.AddSingleton(Config.Configuration.Load);
                services.AddHaselCommon();
                services.AddSingleton<IpfsService>();
                services.AddSingleton<PenumbraService>();
                services.AddSingleton<GlamourerService>();
                services.AddSingleton<CustomizeService>();
                services.AddSingleton<HonorificService>();
                services.AddSingleton<MoodlesService>();
                services.AddSingleton<SimpleHeelsService>();
                services.AddSingleton<PetNameService>();
                services.AddSingleton<PlayerDataService>();
                services.AddSingleton<AntiVirusService>();
                services.AddSingleton<ContextMenuService>();
                services.AddSingleton<ModController>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<SetupWindow>();
                services.AddSingleton<ConfigWindow>();
                services.AddSingleton<OverlayWindow>();
                services.AddSingleton<WindowSystem>();
                services.AddSingleton<DataController>();
                services.AddSingleton<GUIController>();
                services.AddSingleton<ChatController>();
            });
        _host = builder.Build();
        _host.StartAsync();

        _host.Services.GetRequiredService<GUIController>();
        _host.Services.GetRequiredService<ModController>();
        _host.Services.GetRequiredService<DataController>();
        _host.Services.GetRequiredService<ChatController>();
    }
    public void Dispose()
    {
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
    }
}