namespace PocketSizedUniverse.Models.Galaxies;

public class GalaxyMember(string starId, Guid galaxyDataPackId)
{
    public StarPack StarPackReference { get; set; } = new(starId, galaxyDataPackId)
    {
        Type = StarPackType.Galaxy
    };

    public string StarId { get; set; } = starId;
    public GalaxyRole Role { get; set; }
    public DateTime Joined { get; set; } = DateTime.UtcNow;
    public string PublicCertPem { get; set; } = string.Empty;
    public string CertFingerprint { get; set; } = string.Empty;
    public DateTime CertExpiry { get; set; } = DateTime.MinValue;
}