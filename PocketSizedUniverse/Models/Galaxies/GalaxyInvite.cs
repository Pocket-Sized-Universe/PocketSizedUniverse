using PocketSizedUniverse.Models.Data;

namespace PocketSizedUniverse.Models.Galaxies;

public class GalaxyInvite
{
    public Guid InviteId { get; init; }
    public Guid GalaxyId { get; init; }
    public string GalaxyName { get; set; } = string.Empty;
    public string InviteMessage { get; set; } = string.Empty;
    public Guid DataPackId { get; init; }
    public DateTime ExpiresUtc { get; init; }
    public int? MaxUses { get; set; }
    public int CurrentUses { get; set; }
    public string CreatorPublicCert { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string IntroducerId { get; init; } = string.Empty;
    public string ToBase64() => Base64Util.ToBase64(this);
    public string CanonicalData => $"{GalaxyId}|{ExpiresUtc:0}|{MaxUses}|{InviteId}|{DataPackId}|{IntroducerId}";

    public bool IsValid(GalaxyManifest manifest)
    {
        if (DateTime.UtcNow > ExpiresUtc) return false;
        if (MaxUses.HasValue && CurrentUses >= MaxUses) return false;
        var creator = manifest.Members.FirstOrDefault(m => m.StarId == IntroducerId);
        if (creator == null || creator.Status != MemberStatus.Active)
            return false;
        if (creator.Role != GalaxyRole.Admin)
            return false;
        return PsuCertificate.VerifySignature(CreatorPublicCert, CanonicalData, Signature);
    }
    public static GalaxyInvite? FromBase64(string data) => Base64Util.FromBase64<GalaxyInvite>(data);

    public static GalaxyInvite? CreateInvite(GalaxyPack galaxyPack, GalaxyManifest manifest, string message, DateTime expiresUtc)
    {
        return new GalaxyInvite
        {
            InviteId = Guid.NewGuid(),
            GalaxyId = galaxyPack.GalaxyId,
            GalaxyName = manifest.Name,
            InviteMessage = message,
            DataPackId = galaxyPack.DataPackId,
            ExpiresUtc = expiresUtc,
            IntroducerId = galaxyPack.IntroducerId
        };
    }
}