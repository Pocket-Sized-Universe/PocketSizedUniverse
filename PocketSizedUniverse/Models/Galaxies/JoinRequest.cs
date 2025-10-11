using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models.Galaxies;

public class JoinRequest
{
    public Guid RequestId { get; init; }
    public string StarId { get; init; } = string.Empty;
    public string StarName { get; init; } = string.Empty;
    public string PublicCertPem { get; set; } = string.Empty;
    public string CertFingerprint { get; set; } = string.Empty;
    public DateTime CertExpiry { get; set; }
    public Guid InviteId { get; init; }
    public DateTime RequestedUtc { get; init; }
    public string Signature { get; set; } = string.Empty;
    public string CanonicalData => $"{StarId}|{InviteId}|{StarName}|{RequestId}|{RequestedUtc:0}";

    public static JoinRequest Create(Guid inviteId, Star star, PsuCertificate certificate)
    {
        var request = new JoinRequest()
        {
            RequestId = Guid.NewGuid(),
            PublicCertPem = certificate.PublicKey,
            CertExpiry = certificate.Expiry,
            CertFingerprint = certificate.GetFingerprint(),
            InviteId = inviteId,
            StarId = star.StarId,
            StarName = star.Name,
            RequestedUtc = DateTime.UtcNow,
        };
        request.Signature = certificate.SignData(request.CanonicalData);
        return request;
    }
}