using ECommons.DalamudServices;
using Newtonsoft.Json;
using PocketSizedUniverse.Models.Data;
using PocketSizedUniverse.Windows.ViewModels;

namespace PocketSizedUniverse.Models.Galaxies;

public class GalaxyManifest
{
    public int Version { get; set; } = 1;
    public Guid GalaxyId { get; init; }
    public Guid DataPackId { get; init; }
    public string Name { get; set; } = $"{Adjectives.GetRandom()} {Nouns.GetRandom()}";
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public List<GalaxyMember> Members { get; set; } = [];
    public List<ManifestChange> ChangeLog { get; set; } = [];
    public List<GalaxyInvite> Invites { get; set; } = [];
    [JsonIgnore] public string CurrentSignature { get; private set; } = string.Empty;
    public SyncPermissions DefaultPermissions { get; set; } = SyncPermissions.All;

    [JsonIgnore]
    public string CanonicalJson => JsonConvert.SerializeObject(new
    {
        GalaxyId,
        DataPackId,
        Name,
        CreatedUtc,
        Members,
        DefaultPermissions,
        Version
    }, Formatting.None, new JsonSerializerSettings
    {
        DefaultValueHandling = DefaultValueHandling.Include
    });

    public static string GetPath(string dataPackDataPath) => Path.Combine(dataPackDataPath, "GalaxyManifest.json");

    public static GalaxyManifest? LoadFromDisk(string dataPackDataPath)
    {
        var path = GetPath(dataPackDataPath);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonConvert.DeserializeObject<GalaxyManifest>(json);

            if (loaded == null)
                return null;

            var sigPath = path + ".sig";
            if (File.Exists(sigPath))
            {
                loaded.CurrentSignature = File.ReadAllText(sigPath);
            }

            return loaded;
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to load GalaxyManifest: {e}");
            return null;
        }
    }

    public bool SaveToDisk(string dataPackDataPath)
    {
        try
        {
            var path = GetPath(dataPackDataPath);
            var myCert = PsuPlugin.Configuration.MyCertificate;
            if (myCert == null)
                return false;
            var signature = myCert.SignData(CanonicalJson);

            CurrentSignature = signature;
            var base64 = Base64Util.ToBase64(this);
            File.WriteAllText(path, base64);
            var sigPath = path + ".sig";
            File.WriteAllText(sigPath, signature);
            return true;
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to save GalaxyManifest: {e}");
            return false;
        }
    }

    public bool ValidateSignature(string starId, string publicCert)
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentSignature))
            {
                Svc.Log.Warning("Manifest has no signature");
                return false;
            }

            bool isValid = PsuCertificate.VerifySignature(publicCert, CanonicalJson, CurrentSignature);
            if (!isValid)
                Svc.Log.Warning($"Invalid signature from {starId}");
            else
                Svc.Log.Debug($"Valid signature from {starId}");
            return isValid;
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to validate signature: {e}");
            return false;
        }
    }
}