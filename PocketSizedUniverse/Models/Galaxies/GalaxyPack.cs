using Syncthing.Models.Response;

namespace PocketSizedUniverse.Models.Galaxies;

public class GalaxyPack
{
    public string IntroducerId { get; set; } = string.Empty; // StarId of the introducer
    public Guid GalaxyId { get; set; }
    public Guid DataPackId { get; set; }
    public SyncPermissions SyncPermissions { get; set; } = SyncPermissions.All;
    public DataPack? GetDataPack() => PsuPlugin.SyncThingService.DataPacks.GetValueOrDefault(DataPackId);
    public Star? GetIntroducer() => PsuPlugin.SyncThingService.Stars.GetValueOrDefault(IntroducerId);

    public GalaxyManifest? LoadManifest()
    {
        var dataPack = GetDataPack();
        if (dataPack == null)
            return null;
        return GalaxyManifest.LoadFromDisk(dataPack.DataPath);
    }
    
    public IEnumerable<StarPack> AsStarPacks()
    {
        var manifest = LoadManifest();
        if (manifest == null) yield break;
        
        foreach (var member in manifest.Members)
        {
            yield return new StarPack(member.StarId, DataPackId)
            {
                Type = StarPackType.Galaxy,
                GalaxyId = GalaxyId,
                SyncPermissions = SyncPermissions
            };
        }
    }
}