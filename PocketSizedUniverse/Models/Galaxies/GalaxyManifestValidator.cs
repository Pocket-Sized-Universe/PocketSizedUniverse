using ECommons.DalamudServices;

namespace PocketSizedUniverse.Models.Galaxies;

public static class GalaxyManifestValidator
{
    public static ValidationResult Validate(GalaxyManifest manifest)
    {
        if (string.IsNullOrEmpty(manifest.CurrentSignature))
            return ValidationResult.Fail("Manifest is not signed");

        var lastChange = manifest.ChangeLog.OrderByDescending(c => c.Version).FirstOrDefault();
        if (lastChange == null)
            return ValidationResult.Fail("Manifest has no change log");

        var lastSignerId = lastChange.ChangedBy;
        var signer = manifest.Members.FirstOrDefault(m => m.StarId == lastSignerId);
        if (signer == null)
            return ValidationResult.Fail($"Manifest signed by unknown member: {lastSignerId}");

        if (signer.CertExpiry > DateTime.UtcNow)
            return ValidationResult.Fail($"Member {lastSignerId} certificate is expired");

        if (lastChange.ChangeType is ChangeType.MemberAdded or ChangeType.MemberRemoved)
        {
            if (signer.Role != GalaxyRole.Admin && signer.Role != GalaxyRole.Moderator)
                return ValidationResult.Fail(
                    $"Member {lastSignerId} lacks permissions for {lastChange.ChangeType.ToString()}");
        }

        if (lastChange.ChangeType is ChangeType.MemberPermissionsChanged)
        {
            if (signer.Role != GalaxyRole.Admin)
                return ValidationResult.Fail(
                    $"Member {lastSignerId} lacks permissions for {lastChange.ChangeType.ToString()}");
        }

        if (string.IsNullOrEmpty(signer.PublicCertPem))
            return ValidationResult.Fail($"No signature found for signer {lastSignerId}");

        if (!manifest.ValidateSignature(lastSignerId, signer.PublicCertPem))
            return ValidationResult.Fail($"Invalid signature for signer {lastSignerId}");

        Svc.Log.Debug($"Manifest signature verified {lastSignerId} (fingerprint: {signer.CertFingerprint}");
        return ValidationResult.Success();
    }
}