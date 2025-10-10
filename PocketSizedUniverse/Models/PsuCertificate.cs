using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ECommons.DalamudServices;

namespace PocketSizedUniverse.Models;

public class PsuCertificate
{
    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string StarId { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
    public DateTime Created { get; set; }

    public static PsuCertificate Generate(string starId, int validityYears = 10)
    {
        using var rsa = RSA.Create(4096);
        var privateKeyPem = rsa.ExportRSAPrivateKeyPem();

        var publicCertPem = rsa.ExportRSAPublicKeyPem();

        return new PsuCertificate()
        {
            Created = DateTime.UtcNow,
            Expiry = DateTime.UtcNow.AddYears(validityYears),
            PrivateKey = privateKeyPem,
            PublicKey = publicCertPem,
            StarId = starId
        };
    }

    public string SignData(string data)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(PrivateKey);
        
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signature = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return Convert.ToBase64String(signature);
    }

    public static bool VerifySignature(string publicKeyPem, string data, string signature)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = Convert.FromBase64String(signature);

            return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to verify signature: {ex}");
            return false;
        }
    }

    public string GetFingerprint()
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKey);
            
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(publicKeyBytes);
            
            return string.Join(':', hash.Select(b => b.ToString("X2")));
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to get fingerprint: {ex}");
            return string.Empty;
        }
    }
    
    public bool IsExpired() => DateTime.UtcNow > Expiry;
}