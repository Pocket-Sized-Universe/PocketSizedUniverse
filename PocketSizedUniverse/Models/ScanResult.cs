namespace PocketSizedUniverse.Models;

public record ScanResult
{
    public enum ResultType
    {
        Infected = 0,
        Clean = 1,
    }
    public ResultType Result { get; set; }
    public string? MalwareIdentifier { get; set; }
    public DateTime ScanTimeUtc { get; set; } = DateTime.UtcNow;
}