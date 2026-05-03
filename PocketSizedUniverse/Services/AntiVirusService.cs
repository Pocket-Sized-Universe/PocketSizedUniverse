using AntiVirus;
using Microsoft.Extensions.Logging;
using PocketSizedUniverse.Config;

namespace PocketSizedUniverse.Services;

public class AntiVirusService
{
    private readonly ILogger<AntiVirusService> _logger;
    private readonly Configuration _configuration;
    private readonly AntiVirus.Scanner _scanner;
    public AntiVirusService(ILogger<AntiVirusService> logger, Configuration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _scanner = new AntiVirus.Scanner();
    }

    public ScanResult ScanFile(string path)
    {
        var result = _scanner.ScanAndClean(path);
        return result;
    }
}