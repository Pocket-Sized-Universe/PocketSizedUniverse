namespace PocketSizedUniverse.Models.Galaxies;

public class ManifestChange
{
    public int Version { get; set; }
    public DateTime ChangedUtc { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
    public string Details { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;  // Signature of who changed it
}