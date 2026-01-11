using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Mime;
using System.Text;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Ipfs;
using Ipfs.CoreApi;
using Ipfs.Http;
using PocketSizedUniverse.Data;
using PocketSizedUniverse.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Multiformats.Base;
using Newtonsoft.Json.Linq;

namespace PocketSizedUniverse.Services;

public class IpfsService : IDisposable
{
    private const string ZipUrl = "https://dist.ipfs.tech/kubo/v0.39.0/kubo_v0.39.0_windows-amd64.zip";

    private string? ProfilePath => _configuration.CacheDirectory == null
        ? null
        : Path.Combine(_configuration.CacheDirectory, "profile");

    private string? CachePath => _configuration.CacheDirectory == null
        ? null
        : Path.Combine(_configuration.CacheDirectory, "cache");

    private string? ExePath => _configuration.CacheDirectory == null
        ? null
        : Path.Combine(_configuration.CacheDirectory, "bin", "ipfs.exe");

    private Process? _daemonProcess;
    public bool ExePresent => File.Exists(ExePath);
    public IpfsService(ILogger<IpfsService> logger, Config.Configuration configuration,
        IDalamudPluginInterface pluginInterface, IFramework framework)
    {
        _logger = logger;
        _configuration = configuration;
        _pluginInterface = pluginInterface;
        _framework = framework;

        if (CachePath != null && !Directory.Exists(CachePath)) Directory.CreateDirectory(CachePath);
        
        if (_configuration.PairingId == Guid.Empty)
            _configuration.PairingId = Guid.NewGuid();
        
        if (_configuration.IpfsMode != null)
            InitEngine();
    }

    public void InitEngine()
    {
        switch (_configuration.IpfsMode)
        {
            case null:
                break;
            case Config.Configuration.IpfsModeType.Easy:
                InitEasyMode();
                break;
            case Config.Configuration.IpfsModeType.Expert:
                InitExpertMode();
                break;
        }
    }

    private void InitExpertMode()
    {
        Task.Run(async () =>
        {
            try
            {
                var apiUrl = _configuration.IpfsApiUrl ??
                             throw new InvalidOperationException("IPFS API URL is required for expert mode");
                _engine = new IpfsClient(apiUrl);
                if (!await WaitForDaemon())
                {
                    _engine = null;
                    throw new InvalidOperationException("Failed to connect to IPFS daemon.");
                }
                
                if (_configuration.SetRecommendedIpfsConfigs)
                    await SetRecommendedConfigs();
                DaemonIsReady = true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to initialize IPFS engine.");
            }
        });
    }

    public async Task<IEnumerable<string>> GetSubscribedTopics()
    {
        if (_engine == null) return [];
        var result = await _engine.PubSub.SubscribedTopicsAsync();
        return result.Select(s =>
        {
            try
            {
                // Check if it's a multibase string (starts with 'u' for base64url)
                return Encoding.UTF8.GetString(Multibase.Decode(s, out MultibaseEncoding encoding));
            }
            catch
            {
                return s; // Fallback if it's already plain text
            }
        });
    }

    public async Task SubscribeToTopic(string topic, Action<IPublishedMessage> callback, CancellationToken cancel)
    {
        if (_engine == null) return;
        await _engine.PubSub.SubscribeAsync(topic, callback, cancel);
    }
    
    public async Task PublishToTopic(string topic, string data, CancellationToken cancel)
    {
        if (_engine == null) return;
        await _engine.PubSub.PublishAsync(topic, data, cancel);
    }

    private void InitEasyMode()
    {
        Task.Run(async () =>
        {
            try
            {
                if (!ExePresent)
                    throw new InvalidOperationException("IPFS executable not found.");
                
                await StartDaemon();
                _engine = new IpfsClient($"http://localhost:5001");
                if (!await WaitForDaemon())
                {
                    _engine = null;
                    throw new InvalidOperationException("Failed to connect to IPFS daemon.");
                }
                
                await SetRecommendedConfigs();
                DaemonIsReady = true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to initialize IPFS engine.");
            }
        });
    }

    public bool DaemonIsReady = false;
    private async Task SetRecommendedConfigs()
    {
        if (_engine == null) return;
        await _engine.Config.SetAsync("Swarm.ConnMgr.LowWater", 30);
        await _engine.Config.SetAsync("Swarm.ConnMgr.HighWater", 80);
        await _engine.Config.SetAsync("Swarm.ConnMgr.GracePeriod", "60s");
        await _engine.Config.SetAsync("Discovery.MDNS.Enabled", true);
        await _engine.Config.SetAsync("Routing.Type", "autoclient");
        await _engine.Config.SetAsync("AutoNAT.ServiceMode", "enabled");
        await _engine.Config.SetAsync("Swarm.RelayClient.Enabled", true);
        await _engine.Config.SetAsync("Swarm.EnableHolePunching", true);
        await _engine.Config.SetAsync("Ipns.UsePubsub", true);
    }

    private async Task<bool> WaitForDaemon()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancelToken = cts.Token;
        if (_engine == null) throw new InvalidOperationException("IPFS engine not initialized.");
        bool ready = false;
        while (!cancelToken.IsCancellationRequested && !ready)
        {
            try
            {
                var id = await _engine.IdAsync(null, cancelToken);
                if (id == null || !id.IsValid()) throw new InvalidOperationException("Invalid IPFS daemon response.");
                ready = true;
            }
            catch (Exception e)
            {
                _logger.LogDebug("Waiting for IPFS daemon to start...");
                try
                {
                    await Task.Delay(1000, cancelToken);
                }
                catch (OperationCanceledException)
                {
                    // Ignore cancellation here to allow the while loop to exit naturally
                }
            }
        }
        return ready;
    }

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ILogger<IpfsService> _logger;
    private IpfsClient? _engine;
    private readonly Config.Configuration _configuration;
    private readonly IFramework _framework;

    public async Task<bool> DownloadAndExtractBinaries()
    {
        if (ExePath == null) return false;
        if (ExePresent) return true;
        using var client = new HttpClient();
        var zipContent = await client.GetByteArrayAsync(ZipUrl);
        using var zipStream = new MemoryStream(zipContent);
        await using var zipArchive = new ZipArchive(zipStream);
        var exeEntry =
            zipArchive.Entries.FirstOrDefault(e => e.Name.EndsWith("ipfs.exe", StringComparison.OrdinalIgnoreCase));
        if (exeEntry is null)
        {
            _logger.LogError("Failed to find ipfs.exe in zip archive.");
            return false;
        }

        Directory.CreateDirectory(Directory.GetParent(ExePath)?.FullName ?? throw new InvalidOperationException());
        await exeEntry.ExtractToFileAsync(ExePath, true);
        return true;
    }
    
    public async Task<string?> ResolveCidToFilePath(CustomAsset customAsset, CancellationToken cancel = default)
    {
        if (_engine == null) return null;
        while (!cancel.IsCancellationRequested)
        {
            var path = Path.Combine(CachePath, customAsset.Cid + customAsset.Extension);
            if (File.Exists(path)) return path;
            var result = await _engine.FileSystem.ReadFileAsync(customAsset.Cid, cancel);
            {
                var directory = Path.GetDirectoryName(path);
                if (directory == null) throw new InvalidOperationException("Failed to get directory name.");
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                await using var fileStream = File.Create(path);
                await result.CopyToAsync(fileStream, cancel);
            }
            return path;
        }

        return null;
    }

    public async Task<Cid?> AddAndPinFile(string filePath, CancellationToken cancel = default)
    {
        if (_engine == null) return null;
        var options = new AddFileOptions()
        {
            Pin = true
        };
        var result = await _engine.FileSystem.AddFileAsync(filePath, options, cancel);
        if (result is not FileSystemNode node)
        {
            _logger.LogError("Failed to add file: {FilePath}", filePath);
            return null;
        }

        //_logger.LogTrace("Added file: {FilePath} with CID {CID}", filePath, node.Id.ToString());
        return node.Id;
    }

    public void OpenWebUi()
    {
        var psi = new ProcessStartInfo((_configuration.IpfsApiUrl ?? "http://localhost:5001") + "/webui")
        {
            UseShellExecute = true,
            Verb = "open"
        };
        Process.Start(psi);
    }

    public async Task StartDaemon(CancellationToken cancellationToken = default)
    {
        if (ExePath == null) return;
        _daemonProcess = new Process()
        {
            StartInfo = new ProcessStartInfo(ExePath)
            {
                Arguments = "daemon --init --enable-pubsub-experiment --enable-namesys-pubsub",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment = { ["IPFS_PATH"] = ProfilePath }
            }
        };
        _daemonProcess.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                _logger.LogDebug(args.Data);
            }
        };
        _daemonProcess.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                _logger.LogError(args.Data);
            }
        };
        _daemonProcess.Start();
        _daemonProcess.BeginOutputReadLine();
        _daemonProcess.BeginErrorReadLine();
    }

    public Task StopDaemon(CancellationToken cancellationToken = default)
    {
        if (ExePath == null) return Task.CompletedTask;
        if (_daemonProcess is null || _daemonProcess.HasExited) return Task.CompletedTask;
        var shutdownProcess = new Process()
        {
            StartInfo = new ProcessStartInfo(ExePath)
            {
                Arguments = "shutdown",
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { ["IPFS_PATH"] = ProfilePath }
            }
        };
        shutdownProcess.Start();
        shutdownProcess.WaitForExit(5000);
        if (!_daemonProcess.HasExited)
        {
            _daemonProcess.Kill();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopDaemon().Wait();
        _daemonProcess?.Dispose();
        GC.SuppressFinalize(this);
    }
}